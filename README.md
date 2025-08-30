# Unity LTC Decoder

Linear Timecode (LTC) decoder and Timeline synchronization package for Unity.

## Overview

This repository contains a Unity Package that provides real-time LTC (Linear Timecode) decoding from audio input and automatic synchronization with Unity's Timeline system. Perfect for live performances, video production, and any application requiring precise timecode synchronization.

## Features

- Real-time LTC decoding from microphone/audio input
- Automatic Timeline synchronization with configurable thresholds
- Advanced noise filtering and jitter detection
- Custom Inspector UI with real-time monitoring
- Support for various frame rates
- Comprehensive debug logging system

## Installation

### Via Unity Package Manager (Git URL)

1. Open Unity Package Manager (Window > Package Manager)
2. Click the "+" button and select "Add package from git URL..."
3. Enter the following URL:
   ```
   https://github.com/murasaqi/Unity_LTCDecoder.git?path=jp.iridescent.ltcdecoder
   ```

### Via OpenUPM

```bash
openupm add jp.iridescent.ltcdecoder
```

### Manual Installation

1. Clone this repository
2. Copy the `jp.iridescent.ltcdecoder` folder to your Unity project's `Packages` folder

## Quick Start

1. Add `LTCDecoder` component to a GameObject
2. Add `LTCTimelineSync` component to a GameObject with PlayableDirector
3. Link the decoder component to the sync component
4. Select your audio input device in the Inspector
5. Press Play and start your LTC source

## Components

### LTCDecoder
Main component for decoding LTC from audio input. Handles real-time audio processing, timecode validation, and noise filtering.

### LTCTimelineSync
Synchronizes Unity Timeline with decoded LTC. Provides smooth following, automatic synchronization, and signal loss handling.

## Development

The `Unity_LTCDecoderDev~` directory contains a complete Unity project for development and testing. The `~` suffix ensures it's ignored when the package is imported into other Unity projects.

## Requirements

- Unity 2021.3 or higher
- com.unity.timeline 1.6.0 or higher

## License

See [LICENSE](jp.iridescent.ltcdecoder/LICENSE) file for details.

## Author

Murasaqi

## Contributing

Issues and pull requests are welcome!

## Support

For questions and support, please open an issue on GitHub.