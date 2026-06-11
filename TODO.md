# TODO

## Open Loops

- [ ] Verify the packaged Chrome extension flow with a clean real Chrome profile: install app, open bundled extension folder, load the extension, record browser actions, and confirm `/health` reports a recent heartbeat only after the real extension connects.
- [ ] Add an in-app extension installation entry point or guide so test users can find and load the bundled Chrome extension without browsing the install directory manually.
- [ ] Add a release/version bump checklist or script so installer filename, Inno `AppVersion`, and .NET executable metadata stay synchronized for every new test package.
