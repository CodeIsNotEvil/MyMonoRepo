# YTerMusic

Terminal Application for listening to YouTube Music

Made by ccgauche and available throgh [github](https://github.com/ccgauche/ytermusic?tab=readme-ov-file)

## Installation

### Dependencies

```bash
sudo apt install alsa-tools libasound2-dev libdbus-1-dev pkg-config
```

### Install

via cargo:

```bash
 cargo install ytermusic --git https://github.com/ccgauche/ytermusic
```

If it fails you might need to seht the CARGO_TARGET_DIR

example in nushell:

```bash
CARGO_TARGET_DIR=/tmp/cargo-install64F9AS $env.CARGO_TARGET_DIR
```

### Setup

The Application need the Auth cookie to connect, just follow the instructions inside the terminal.

If Firefox is used you need to check the raw toggle to copy the Cookie header into the header.txt file.
