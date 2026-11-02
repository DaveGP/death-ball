# Death Ball

## Overview

**Death Ball** is a brutal, futuristic 4v4 arena sport.

Two teams fight over a single steel ball inside a sealed arena. Win by scoring more than the other team, or by putting all four of their players on the floor.

## Features

* 4v4 arena sport with full-contact combat
* Two ways to win: score goals or knock out the whole opposing team
* Swap control between teammates on the fly
* Any player slot can be local, couch multiplayer, online or AI
* League mode with injuries, team management and progression

## Development

### Technology Stack

| Technology      | Details       |
| --------------- | ------------- |
| Engine          | Unity 6       |
| Language        | C#            |
| Version Control | Git + Git LFS |
| Networking      | FishNet       |

## Project Structure

```text
death-ball/
│
├── Unity/                   # The Unity Project
├──── Assets/                  # Unity assets and game content
├──── Packages/                # Unity package dependencies
├──── ProjectSettings/         # Unity configuration
│
├── Documentation/           # Design and technical documentation
│
├── README.md
├── .gitignore
└── .gitattributes
```

## Getting Started

### Requirements

Before opening the project, ensure you have:

* Unity 6 installed
* Git installed
* Git LFS installed

### Installation

Clone the repository:

```bash
git clone https://github.com/DaveGP/death-ball.git
```

Install Git LFS:

```bash
git lfs install
```

Open the project using Unity Hub and select the correct Unity version.

## Documentation

Additional documentation can be found in the `Documentation` folder.

* [Game Design Document](Documentation/GameDesign.md)
* [Technical Design](Documentation/TechnicalDesign.md)
* [Roadmap](Documentation/Roadmap.md)
* [Decisions](Documentation/Decisions.md)

## Credits

Created by David Pearson.

## License

This project is currently private and all rights reserved.
