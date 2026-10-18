using System.Globalization;
using System.Runtime.CompilerServices;
using FluentValidation;

namespace TurnoSync.Tests.Support;

internal static class TestEnvironment
{
    /// <summary>
    /// La cultura de FluentValidation es global. La API la fija en español al arrancar; aquí se hace lo mismo para las
    /// pruebas que no pasan por Program, si no los mensajes dependerían del idioma de la máquina y del orden de ejecución.
    /// </summary>
#pragma warning disable CA2255 // Proyecto de pruebas, no una biblioteca
    [ModuleInitializer]
#pragma warning restore CA2255
    internal static void Initialize() =>
        ValidatorOptions.Global.LanguageManager.Culture = new CultureInfo("es");
}
