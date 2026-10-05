## Summary

<!-- What does this pull request change, and why? Link related issues with "Fixes #123". -->

## Type of change

- [ ] 🐛 Bug fix
- [ ] ✨ New feature or support for another EDC15 variant
- [ ] 🎨 User interface
- [ ] 📝 Documentation
- [ ] ♻️ Refactor / cleanup

## Testing

<!-- How did you test this? Changes to how the dump is read or written need a unit test. -->

- [ ] `dotnet build -c Release` has no warnings
- [ ] `dotnet test` passes
- ECU tested on: <!-- e.g. Bosch 0281010390, EDC15P+, VW Golf IV 2002 — or "not tested on an ECU" -->

## Checklist

- [ ] I followed the existing code style
- [ ] I updated the README where needed
- [ ] New offsets come with a source (datasheet, documented dump or tested ECU)
- [ ] No EEPROM dumps, VINs, immobilizer IDs or login codes are included
