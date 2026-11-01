# Creating projects with the .NET CLI

Replace the placeholders with your own values:

- `<Solution>` – solution name (e.g. `MyApp`)
- `<Project>` – project name (e.g. `MyApp.Core`)
- `<Project>.Tests` – matching test project name

## 1. Create a solution (once)

```powershell
dotnet new sln -n <Solution>
```

Example:

```powershell
dotnet new sln -n MyApp
```

## 2. Create a project

```powershell
dotnet new <template> -n <Project> -o src/<Project>
dotnet sln add src/<Project>
```

Common templates:

| Template    | Purpose                |
|-------------|------------------------|
| `classlib`  | Class library          |
| `console`   | Console app            |
| `webapi`    | ASP.NET Core Web API   |
| `worker`    | Background worker      |

Run `dotnet new list` to see all available templates.

Useful options:

```powershell
-f net8.0          # target framework
-lang "C#"         # language (default is C#)
```

Examples:

```powershell
# Class library
dotnet new classlib -n MyApp.Core -o src/MyApp.Core
dotnet sln add src/MyApp.Core

# Console app targeting .NET 8
dotnet new console -n MyApp.Cli -o src/MyApp.Cli -f net8.0
dotnet sln add src/MyApp.Cli

# Web API
dotnet new webapi -n MyApp.Api -o src/MyApp.Api
dotnet sln add src/MyApp.Api
```

## 3. Create a test project

Test templates: `xunit`, `nunit`, `mstest`.

```powershell
dotnet new xunit -n <Project>.Tests -o tests/<Project>.Tests
dotnet add tests/<Project>.Tests reference src/<Project>
dotnet sln add tests/<Project>.Tests
```

Example (tests for `MyApp.Core`):

```powershell
dotnet new xunit -n MyApp.Core.Tests -o tests/MyApp.Core.Tests
dotnet add tests/MyApp.Core.Tests reference src/MyApp.Core
dotnet sln add tests/MyApp.Core.Tests
```

Example test (`tests/MyApp.Core.Tests/CalculatorTests.cs`):

```csharp
using MyApp.Core;

public class CalculatorTests
{
    [Fact]
    public void Add_ReturnsSum()
    {
        var calculator = new Calculator();

        Assert.Equal(5, calculator.Add(2, 3));
    }
}
```

Using NUnit or MSTest instead:

```powershell
dotnet new nunit -n MyApp.Core.Tests -o tests/MyApp.Core.Tests
dotnet new mstest -n MyApp.Core.Tests -o tests/MyApp.Core.Tests
```

## 4. Reference other projects and packages

```powershell
dotnet add <ProjectPath> reference <OtherProjectPath>   # project reference
dotnet add <ProjectPath> package <PackageName>          # NuGet package
```

Examples:

```powershell
# Make the API use the core library
dotnet add src/MyApp.Api reference src/MyApp.Core

# Add a NuGet package to a project
dotnet add src/MyApp.Core package Newtonsoft.Json

# Add a specific package version
dotnet add src/MyApp.Core package Serilog --version 3.1.1

# Add a mocking library to the test project
dotnet add tests/MyApp.Core.Tests package NSubstitute
```

## 5. Build and test

```powershell
dotnet build                                        # build the solution
dotnet test                                         # run all tests
dotnet test tests/<Project>.Tests                   # run one test project
dotnet test --filter "FullyQualifiedName~<Name>"    # run matching tests only
dotnet test --collect:"XPlat Code Coverage"         # with code coverage
```

Examples:

```powershell
dotnet build -c Release
dotnet test tests/MyApp.Core.Tests
dotnet test --filter "FullyQualifiedName~CalculatorTests"
dotnet test --filter "FullyQualifiedName=MyApp.Core.Tests.CalculatorTests.Add_ReturnsSum"
```

## Suggested layout

```
<Solution>.sln
src/
  <Project>/
tests/
  <Project>.Tests/
```

Example:

```
MyApp.sln
src/
  MyApp.Core/
  MyApp.Api/
tests/
  MyApp.Core.Tests/
  MyApp.Api.Tests/
```

## End-to-end example

Create a solution with a library and its tests, then build and run the tests:

```powershell
dotnet new sln -n MyApp

dotnet new classlib -n MyApp.Core -o src/MyApp.Core
dotnet new xunit    -n MyApp.Core.Tests -o tests/MyApp.Core.Tests

dotnet sln add src/MyApp.Core tests/MyApp.Core.Tests
dotnet add tests/MyApp.Core.Tests reference src/MyApp.Core

dotnet build
dotnet test
```
