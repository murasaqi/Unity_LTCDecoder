//
// LTC (Linear timecode) data structure and decoder
//

using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace jp.iridescent.ltcdecoder
{
    // Unpacked representation of LTC
    public class Timecode
    {
        public int Frame = 0;
        public int Second = 0;
        public int Minute = 0;
        public int Hour = 0;
        public bool DropFrame = false;

        // Unpack LTC data into a timecode instance
        static public Timecode Unpack(ulong data)
        {
            var s1 = (int)((data      ) & 0xffff);
            var s2 = (int)((data >> 16) & 0xffff);
            var s3 = (int)((data >> 32) & 0xffff);
            var s4 = (int)((data >> 48) & 0xffff);

            return new Timecode
            {
                Frame  = (s1 & 0xf) + ((s1 >> 8) & 3) * 10,
                Second = (s2 & 0xf) + ((s2 >> 8) & 7) * 10,
                Minute = (s3 & 0xf) + ((s3 >> 8) & 7) * 10,
                Hour   = (s4 & 0xf) + ((s4 >> 8) & 3) * 10,
                DropFrame = (s1 & 0x400) != 0
            };
        }
        
        static public Timecode FromBytes(byte[] data)
        {
            var s1 = (int)((data[0]      ) & 0xff) + ((data[1] & 0x3f) << 8);
            var s2 = (int)((data[2]      ) & 0xff) + ((data[3] & 0x3f) << 8);
            var s3 = (int)((data[4]      ) & 0xff) + ((data[5] & 0x3f) << 8);
            var s4 = (int)((data[6]      ) & 0xff) + ((data[7] & 0x3f) << 8);

            return new Timecode
            {
                Frame  = (s1 & 0xf) + ((s1 >> 8) & 3) * 10,
                Second = (s2 & 0xf) + ((s2 >> 8) & 7) * 10,
                Minute = (s3 & 0xf) + ((s3 >> 8) & 7) * 10,
                Hour   = (s4 & 0xf) + ((s4 >> 8) & 3) * 10,
                DropFrame = (s1 & 0x400) != 0
            };
        }

        public void AddTime(int frame)
        {
            if (frame < 0)
            {
                SubtractTime(-frame);
                return;
            }
            
            Frame += frame;
            if (Frame >= 30)
            {
                Frame = 0;
                Second++;
                if (Second >= 60)
                {
                    Second = 0;
                    Minute++;
                    if (Minute >= 60)
                    {
                        Minute = 0;
                        Hour++;
                        if (Hour >= 24)
                        {
                            Hour = 0;
                        }
                    }
                }
            }
        }
        
        public void SubtractTime(int frame)
        {
            if (frame < 0)
            {
                AddTime(-frame);
                return;
            }
            
            Frame -= frame;
            if (Frame < 0)
            {
                Frame = 29;
                Second--;
                if (Second < 0)
                {
                    Second = 59;
                    Minute--;
                    if (Minute < 0)
                    {
                        Minute = 59;
                        Hour--;
                        if (Hour < 0)
                        {
                            Hour = 23;
                        }
                    }
                }
            }
        }

        public override string ToString()
        {
            return $"{Hour:D2}:{Minute:D2}:{Second:D2}:{Frame:D2}";
        }

        public int GetFrameCount()
        {
            var totalFrames = Frame;
            totalFrames += Second * 30;
            totalFrames += Minute * 30 * 60;
            totalFrames += Hour * 30 * 60 * 60;
            return totalFrames;
        }

        public int Difference(Timecode other)
        {
            return GetFrameCount() - other.GetFrameCount();
        }

        public void Reset()
        {
            Frame = 0;
            Second = 0;
            Minute = 0;
            Hour = 0;
            DropFrame = false;
        }
        
        public bool Equals(Timecode other)
        {
            if (other == null)
                return false;

            var res = Frame == other.Frame &&
                      Second == other.Second &&
                      Minute == other.Minute &&
                      Hour == other.Hour;

            return res;
        }
        
        public void Set(Timecode other)
        {
            Frame = other.Frame;
            Second = other.Second;
            Minute = other.Minute;
            Hour = other.Hour;
            DropFrame = other.DropFrame;
        }

        public byte[] AsBytes()
        {
            var s1 = (Frame % 10) | ((Frame / 10) << 8);
            var s2 = (Second % 10) | ((Second / 10) << 8);
            var s3 = (Minute % 10) | ((Minute / 10) << 8);
            var s4 = (Hour % 10) | ((Hour / 10) << 8);

            if (DropFrame)
                s1 |= 0x400;

            return new[]
            {
                (byte)(s1 & 0xff), (byte)((s1 >> 8) & 0xff),
                (byte)(s2 & 0xff), (byte)((s2 >> 8) & 0xff),
                (byte)(s3 & 0xff), (byte)((s3 >> 8) & 0xff),
                (byte)(s4 & 0xff), (byte)((s4 >> 8) & 0xff)
            };
        }
    }

    // Timecode decoder class that analyzes audio signals to extract LTC data
    public sealed class TimecodeDecoder
    {
        #region Public methods

        public Timecode LastTimecode { get; private set; }

        public void ParseAudioData(ReadOnlySpan<float> data)
        {
            foreach (var v in data) ProcessSample(v > 0.0f);
        }

        #endregion

        #region Internal state

        // 128 bit FIFO queue for the bit stream
        (ulong lo, ulong hi) _fifo;

        // Sample count from the last transition
        int _count;

        // Bit period (x100 fixed point value)
        int _period;

        // Transition counter for true bits
        bool _tick;

        // Current state (the previous sample value)
        bool _state;

        #endregion

        #region Private methods

        void ProcessSample(bool sample)
        {
            // Biphase mark code decoder with an adaptive bit-period estimator.

            // No transition?
            if (_state == sample)
            {
                // Just increment the counter.
                if (_count < 10000) _count++;
                return;
            }

            // Half period?
            if (_count < _period / 100)
            {
                // Second transition?
                if (_tick)
                {
                    ProcessBit(true); // Output: "1"
                    _tick = false;
                }
                else
                    _tick = true;
            }
            else
            {
                ProcessBit(false); // Output: "0"
                _tick = false;
            }

            // Adaptive estimation of the bit period
            _period = (_period * 99 + _count * 100) / 100;

            _state = sample;
            _count = 0;
        }

        void ProcessBit(bool bit)
        {
            const ushort sync = 0xbffc; // LTC sync word
            const ulong msb64 = 1ul << 63;

            // 64:64 combined FIFO
            var hi_lsb = (_fifo.hi & 1ul) != 0;
            _fifo.lo = (_fifo.lo >> 1) | (hi_lsb ? msb64 : 0ul);
            _fifo.hi = (_fifo.hi >> 1) | (   bit ? msb64 : 0ul);

            // LTC sync word detection
            if ((ushort)_fifo.hi == sync)
                LastTimecode = Timecode.Unpack(_fifo.lo);
        }

        #endregion
    }

    public sealed class TimecodeEncoder
    {
        //timecodeをLTCのデータに変換する
        //0bit : Frame 1
        //1bit : Frame 2
        //2bit : Frame 4
        //3bit : Frame 8
        //4bit : User Bit Field1
        //5bit : User Bit Field1
        //6bit : User Bit Field1
        //7bit : User Bit Field1
        //8bit : Frame 10
        //9bit : Frame 20
        //10bit : dropFrame flag
        //11bit : colorFrame flag
        //12bit : User Bit Field2
        //13bit : User Bit Field2
        //14bit : User Bit Field2
        //15bit : User Bit Field2

        //16bit : Second 1
        //17bit : Second 2
        //18bit : Second 4
        //19bit : Second 8
        //20bit : User Bit Field3
        //21bit : User Bit Field3
        //22bit : User Bit Field3
        //23bit : User Bit Field3
        //24bit : Second 10
        //25bit : Second 20
        //26bit : Second 40
        //27bit : flag
        //28bit : User Bit Field4
        //29bit : User Bit Field4
        //30bit : User Bit Field4
        //31bit : User Bit Field4

        //32bit : Minute 1
        //33bit : Minute 2
        //34bit : Minute 4
        //35bit : Minute 8
        //36bit : User Bit Field5
        //37bit : User Bit Field5
        //38bit : User Bit Field5
        //39bit : User Bit Field5
        //40bit : Minute 10
        //41bit : Minute 20
        //42bit : Minute 40
        //43bit : flag
        //44bit : User Bit Field6
        //45bit : User Bit Field6
        //46bit : User Bit Field6
        //47bit : User Bit Field6

        //48bit : Hour 1
        //49bit : Hour 2
        //50bit : Hour 4
        //51bit : Hour 8
        //52bit : User Bit Field7
        //53bit : User Bit Field7
        //54bit : User Bit Field7
        //55bit : User Bit Field7
        //56bit : Hour 10
        //57bit : Hour 20
        //58bit : clock flag
        //59bit : flag
        //60bit : User Bit Field8
        //61bit : User Bit Field8
        //62bit : User Bit Field8
        //63bit : User Bit Field8

        //64bit-79bit : 0xbffc sync word

        public static ulong Pack(Timecode timecode)
        {
            var s1 = (ushort)(timecode.Frame % 10);
            s1 |= (ushort)((timecode.Frame / 10 % 10) << 8);
            s1 |= (ushort)(timecode.DropFrame ? 0x400 : 0);

            var s2 = (ushort)(timecode.Second % 10);
            s2 |= (ushort)((timecode.Second / 10 % 6) << 8);

            var s3 = (ushort)(timecode.Minute % 10);
            s3 |= (ushort)((timecode.Minute / 10 % 6) << 8);

            var s4 = (ushort)(timecode.Hour % 10);
            s4 |= (ushort)((timecode.Hour / 10 % 4) << 8);
            s4 |= (ushort)(timecode.DropFrame ? 0x4000 : 0);

            return (ulong)s1 | ((ulong)s2 << 16) | ((ulong)s3 << 32) | ((ulong)s4 << 48);
        }
        

        //1TC分の音声データを作成する
        public static bool Encode(ref NativeArray<float> _outAudioBuffer, Timecode timecode, int fps = 30, bool firstPhase = false, int sampleRate = 48000)
        {
            // Debug.Log($"encode {timecode}");
            
            var bitSampleCount = sampleRate / (80 * fps);  //48000Hz 30fpsの場合1bitを表すのに20サンプル必要
            // var tcSampleCount = bitSampleCount * 80;  //1tcにつき80bitで表されるので 48000Hz 30fpsの場合1tcを表すのに1600サンプル必要
            // Debug.Log(sampleCount);

            var packedData = Pack(timecode);
            const ulong syncWord = 0xbffc;

            var phase = firstPhase;
            for (var i = 0; i < 64; i++)
            {
                var bit = (packedData & (1ul << i)) != 0;
                for (var j = 0; j < bitSampleCount; j++)
                {
                    //packedDataのiビット目を取得
                    if (bit)
                    {
                        //1の場合
                        if (j < bitSampleCount / 2)
                            _outAudioBuffer[i * bitSampleCount + j] = !phase ? 1.0f : -1.0f;
                        else
                            _outAudioBuffer[i * bitSampleCount + j] = !phase ? -1.0f : 1.0f;
                    }
                    else
                    {
                        //0の場合
                        _outAudioBuffer[i * bitSampleCount + j] = !phase ? 1.0f : -1.0f;
                    }
                }

                if(!bit)
                    phase = !phase;
            }

            for (var i = 0; i < 16; i++)
            {
                var bit = (syncWord & (1ul << i)) != 0;
                for (var j = 0; j < bitSampleCount; j++)
                {
                    if (bit)
                    {
                        if(j < bitSampleCount / 2)
                            _outAudioBuffer[64 * bitSampleCount + i * bitSampleCount + j] = !phase ? 1.0f : -1.0f;
                        else
                            _outAudioBuffer[64 * bitSampleCount + i * bitSampleCount + j] = !phase ? -1.0f : 1.0f;
                    }
                    else
                    {
                        _outAudioBuffer[64 * bitSampleCount + i * bitSampleCount + j] = !phase ? 1.0f : -1.0f;
                    }
                }

                if(!bit)
                    phase = !phase;
            }

            return phase;
        }
    }
}
