# AGENTS.md

Follow `C:\code\agents\requirements` when editing this repository.

## Build

```powershell
dotnet build src/Xeno.sln
dotnet run --project src/Test.Automated -- --results results.json
dotnet test src/Test.Xunit
dotnet test src/Test.Nunit
```

## Style

- No `var`.
- No tuple returns.
- `using` statements belong inside namespace blocks.
- Public APIs need XML documentation.
- Async methods that perform cancellable work accept `CancellationToken`.
- Use `ConfigureAwait(false)` in library/server code.

## Testing

Shared test descriptors live in `src/Test.Shared` and run through Touchstone:

- `src/Test.Automated`
- `src/Test.Xunit`
- `src/Test.Nunit`
