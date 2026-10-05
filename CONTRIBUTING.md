# Contributing to the Bosch EDC15 EEPROM Tool

Thank you for taking the time to contribute! Every tested ECU, bug report, fix and idea makes this tool better for everyone who works on EDC15 control units.

This project follows the [Code of Conduct](CODE_OF_CONDUCT.md). By taking part, you agree to uphold it.

## Ways to Contribute

| | |
|---|---|
| 🚗 **Report a tested ECU** | Tell us which ECU (Bosch part number, e.g. `0281010xxx`) and vehicle the tool worked on — or did not. |
| 🐛 **Report a bug** | Describe what you did, what you expected and what happened. |
| 💡 **Suggest a feature** | Explain the workshop task it would help with. |
| 🔧 **Submit code** | Fixes, support for further EDC15 variants, user-interface improvements. |
| 📝 **Improve the docs** | Clearer explanations, corrections, translations of the README. |

## Reporting Bugs

Before opening an issue, please search the [existing issues](https://github.com/muki01/Bosch_EDC15_EEPROM_Tool/issues), then pick the matching form on the [new issue](https://github.com/muki01/Bosch_EDC15_EEPROM_Tool/issues/new/choose) page: **Bug report**, **ECU report** or **Feature request**. A good report includes:

- The tool version (shown in the title bar)
- Windows version
- The ECU: Bosch part number, software version and vehicle
- What the tool showed: immobilizer, mileage, login code, VIN and any warning
- Steps to reproduce the problem

> [!IMPORTANT]
> **Do not attach EEPROM dumps publicly.** A dump contains the VIN, the immobilizer ID and the login code of a real vehicle. If a dump is needed to reproduce a problem, offer to send it privately, or replace these values first.

Found a security problem? Please follow the [Security Policy](SECURITY.md) and report it privately instead of opening an issue.

## Development Workflow

1. **Fork** the repository and create a branch from `main`:
   ```bash
   git checkout -b feature/my-improvement
   ```
2. Make your changes, keeping them **focused**: one fix or feature per pull request.
3. Run the tests and make sure the build has no warnings:
   ```bash
   dotnet build -c Release
   dotnet test
   ```
4. **Add a test** when you change how the dump is read or written. The tests in `tests/` check every edit against the example dumps byte for byte.
5. Commit with a clear message, e.g. `Show a warning when the login code copies differ`.
6. Push and open a **pull request** against `main`. The pull request template asks what you changed and which ECU you tested on, and GitHub Actions builds and tests it automatically.

## Coding Guidelines

- Follow the existing style of the file you are editing: naming, indentation and comment density.
- Keep the layers apart: `Core/Edc15Eeprom.cs` reads and writes bytes and has no UI code; `ViewModels/` holds the state of the window; the XAML only displays it.
- Every value in the dump is stored twice. Read the first copy, write both, and never change bytes the user did not edit.
- New offsets need a source (a datasheet, a documented dump or a tested ECU) mentioned in the pull request.
- User-facing text is English, short and plain.

## License

By contributing, you agree that your contributions are licensed under the [GNU General Public License v3.0](LICENSE), and that the author may also offer them under a commercial license.
