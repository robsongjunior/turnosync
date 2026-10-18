using System.Globalization;
using System.Runtime.CompilerServices;
using FluentValidation;

namespace TurnoSync.Tests.Support;

internal static class TestEnvironment
{
    /// <summary>
    /// The FluentValidation culture is global. The API pins it to English at startup; we do the same here for
    /// tests that do not go through Program, otherwise the messages would depend on the machine language and on execution order.
    /// </summary>
#pragma warning disable CA2255 // Test project, not a library
    [ModuleInitializer]
#pragma warning restore CA2255
    internal static void Initialize() =>
        ValidatorOptions.Global.LanguageManager.Culture = new CultureInfo("en-US");
}
