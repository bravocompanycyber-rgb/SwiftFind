# Adding the official SwiftFind.ico

The multi-resolution Windows icon is generated and kept with the brand assets.

## Quick add (recommended)

1. Download `SwiftFind.ico` from the design package (or the file produced in CI).
2. Place it at:

```
assets/SwiftFind.ico
```

3. Commit and push:

```bash
git add assets/SwiftFind.ico
git commit -m "Add official multi-resolution application icon"
git push
```

The `.csproj` will automatically pick it up as `ApplicationIcon` on the next build.

Even without the `.ico` file, the running application still shows the correct premium mark because `Helpers/AppIcon.cs` generates it at runtime and `Program.cs` assigns it to the main form.
