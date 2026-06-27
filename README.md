# Trimmer

A PHP-like developer experience for C#. Drop `.cshtml` and `.cs` files in a folder and
serve them as Razor pages. No `.csproj`, no `.sln`, no `appsettings.json` - just the files
your app is actually made of.

Trimmer is to Razor what `index.php` is to PHP: write a page, hit the URL, see the result.
C# inside your pages runs on the server and returns HTML, exactly like PHP - except the
language is C# and the code lives in `@code` blocks.

---

## Features

- **Zero project files.** A Trimmer project is just `.cshtml` pages, optional `.cs` helpers
  and static assets. There is nothing else to configure.
- **Pages are Razor.** Inline expressions (`@(1 + 2)`), control flow (`@foreach`, `@if`) and
  class members in `@code` / `@functions` blocks all work.
- **Shared C# code.** Any `.cs` file in the project is compiled alongside your pages; bring
  its types into a page with a normal `@using`.
- **Clean URLs.** `/login` serves `login.cshtml`, `/` serves `index.cshtml`, `/blog/` serves
  `blog/index.cshtml`. Static files (images, CSS, JS) are served as-is.
- **NuGet on demand.** Reference a package from inside any file using the `dotnet run`
  directive syntax: `#:package Humanizer@2.14.1`. Trimmer restores it for you.
- **Hot reload.** Edit a file and the browser refreshes itself - no build step, no restart.
- **Production build.** `trimmer build` compiles the whole site into a single compact
  bundle, and `trimmer run` serves that bundle with everything pre-compiled.
- **`.cs` files are never exposed.** Source files are compiled, not served.

---

## Installation

Trimmer is a .NET global tool and requires the **.NET 10 SDK**.

### From source

```sh
git clone https://github.com/DomJob/trimmer
cd trimmer
dotnet pack Trimmer/Trimmer.csproj -c Release
dotnet tool install --global --add-source ./Trimmer/bin/Release Trimmer
```

Once installed, the `trimmer` command is available on your `PATH`. Remove it with:

```sh
dotnet tool uninstall --global Trimmer
```

---

## Quick start

Create a folder and add a page:

```
mkdir www && cd www
```

`www/index.cshtml`:

```cshtml
@code {
    string Name => "world";
}
<!doctype html>
<html>
  <body>
    <h1>Hello, @Name!</h1>
    <p>2 + 2 = @(2 + 2)</p>
  </body>
</html>
```

Serve it:

```sh
trimmer serve
```

Open <http://localhost:5122>. Edit the file and watch the page reload itself.

---

## Project layout

A project is just files. There are no required files and no fixed names other than
`index.cshtml` being the default document for a directory.

```
www/
├── index.cshtml        ->  /
├── login.cshtml        ->  /login
├── blog/
│   └── index.cshtml    ->  /blog
├── site.cs             ->  shared C#, not served
└── assets/
    ├── logo.png        ->  /assets/logo.png
    └── app.js          ->  /assets/app.js
```

A complete example lives in [`examples/hello-world`](examples/hello-world).

---

## Writing pages

### Inline C# and control flow

```cshtml
<ul>
@for (var i = 1; i <= 3; i++)
{
    <li>Item @i</li>
}
</ul>
```

### Code blocks

`@code` and `@functions` blocks become members of the page class:

```cshtml
@code {
    int Square(int n) => n * n;
}
<p>@Square(8)</p>
```

### Sharing code from `.cs` files

Put reusable logic in a `.cs` file:

```csharp
// site.cs
namespace Site;

public static class Clock
{
    public static string Today() => DateTime.Now.ToString("D");
}
```

Use it from any page:

```cshtml
@using Site
<p>Today is @Clock.Today()</p>
```

### HTML encoding

Expressions are HTML-encoded by default. To emit pre-built markup verbatim, wrap it with
`@Raw(...)`:

```cshtml
<p>@Raw("<strong>bold</strong>")</p>
```

### Using NuGet packages

Reference a package at the top of any `.cshtml` or `.cs` file using the same directive
`dotnet run` uses for file-based apps:

```cshtml
#:package Humanizer@2.14.1
@using Humanizer
<p>You have @(3.ToWords()) new messages.</p>
```

The version is optional (`#:package Humanizer` takes the latest). Packages are restored the
first time you serve or build, and cached afterwards.

---

## Commands

```
trimmer serve [dir] [--port <n>]        Serve a project with hot reload (default port 5122).
trimmer build [dir] [--output <file>]   Compile a project into a compact .trm bundle.
trimmer run <bundle> [--port <n>]       Serve a pre-built bundle (default port 5122).
trimmer help                            Show help.
```

### serve

Serves a project directly from disk and recompiles pages as you edit them.

```sh
trimmer serve ./www --port 8080
```

### build

Compiles every page and `.cs` file into a single assembly, bundles it with the resolved
NuGet assemblies and your static assets, and writes one `.trm` file.

```sh
trimmer build ./www --output site.trm
```

The bundle defaults to `<foldername>.trm` when `--output` is omitted.

### run

Serves a `.trm` bundle with all pages pre-compiled and no file watching - intended for
production.

```sh
trimmer run site.trm --port 5122
```

---

## How it works

- **Razor → C#.** Each `.cshtml` file is parsed by the Razor language engine into a C# class
  that derives from `TrimmerPage`. `@code` blocks are treated like Razor `@functions`.
- **C# → assembly.** The generated page classes and your `.cs` files are compiled together
  with Roslyn into an in-memory assembly (or to disk for `build`).
- **Packages.** `#:package` directives are parsed out of your files, restored through the
  .NET SDK (transitive dependencies included), cached under `~/.trimmer`, and added both as
  compile-time references and runtime probing paths.
- **Serving.** A minimal Kestrel host maps each request to a file: `.cshtml` is rendered,
  `.cs` is forbidden, and everything else is served as a static asset.
- **Hot reload.** A file-system watcher invalidates the compiled-page cache and pushes a
  reload over Server-Sent Events to a tiny script injected into every HTML response.

---

## Building and testing Trimmer itself

```sh
dotnet build              # build the tool and tests
dotnet test               # run the unit test suite
dotnet test --filter Category=Integration   # also restore a real NuGet package (needs network)
```

The solution contains two projects:

- `Trimmer` - the tool, published as a .NET global tool.
- `Trimmer.Tests` - the NUnit test suite.

---

## License

MIT
