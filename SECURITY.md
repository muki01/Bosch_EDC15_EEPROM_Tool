# Security Policy

This tool changes the immobilizer status, login code and mileage stored in the EEPROM of an engine control unit. Security reports are therefore taken seriously.

## Supported Versions

Security fixes are applied to the latest code on the `main` branch and to the most recent release.

## Reporting a Vulnerability

If you find a security issue, **please do not open a public issue**.

Instead, email **muksin.muksin04@gmail.com** or use [private vulnerability reporting](https://github.com/muki01/Bosch_EDC15_EEPROM_Tool/security/advisories/new) with:

- A description of the issue and its potential impact
- Steps to reproduce (tool version, Windows version, ECU type)
- A suggested fix, if you have one

Please **do not send a full EEPROM dump** unless it is needed to reproduce the problem. If it is, replace the VIN and immobilizer ID first or tell us and we will agree on a private way to share it.

You will receive a response as soon as possible, and credit in the release notes if you wish.

## Downloads

- Only download the program from the [Releases](https://github.com/muki01/Bosch_EDC15_EEPROM_Tool/releases) page of this repository. Copies from other sites may be modified.
- The executable is not code-signed, so Windows SmartScreen may show a warning the first time it runs. If you prefer, build the program from source as described in the README.

## Notes for Users

- Always keep the original dump. The tool suggests a new file name when saving, but a backup on another drive is the safest choice.
- An EEPROM dump contains the VIN, immobilizer ID and login code of a real car. Treat it like a key and do not share it in public issues or forums.
- Use the tool only on vehicles you own or are authorised to work on, and in line with local law. Changing the recorded mileage of a vehicle to deceive a buyer is illegal in most countries.
