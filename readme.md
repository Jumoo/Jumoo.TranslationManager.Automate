# Jumoo.TranslationManager.Automate

[![Jumoo.TranslationManager.Automate](https://img.shields.io/nuget/vpre/Jumoo.TranslationManager.Automate?label=Jumoo.TranslationManager.Automate)](https://www.nuget.org/packages/Jumoo.TranslationManager.Automate)

Translation Manager support for [Umbraco.Automate](https://jumoo.co.uk/) — create, submit and
approve Translation Manager jobs from Umbraco Automate workflows, and start workflows from
translation events (job submitted, received, approved, published).

The classic use case: **when content is published, translate it** — wire
`Content Published` → `Translate Content` → `Check Translation Job` → `Approve Translation Job`
into an automation, no code required.

## Status

Pre-release — not yet published to NuGet.

## What's in this package

- **Actions** — `Translate Content`, `Check Translation Job`, `Approve Translation Job`.
- **Triggers** — `Translation Job Submitted`, `Translation Received`, `Translation Job Approved`,
  `Translation Published`.

See [docs/implementation-plan.md](docs/implementation-plan.md) for the full design, including the
loop-protection rules that make it safe to chain `Content Published` back into `Translate Content`.

## Building

```
dotnet build Jumoo.TranslationManager.Automate.slnx
dotnet test Jumoo.TranslationManager.Automate.slnx
```

## License

[MPL-2.0](LICENSE)
