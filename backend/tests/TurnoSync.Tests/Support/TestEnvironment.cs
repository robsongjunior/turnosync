using System.Globalization;
using System.Runtime.CompilerServices;
using FluentValidation;

namespace TurnoSync.Tests.Support;

internal static class TestEnvironment
{
    /// <summary>
    /// A cultura do FluentValidation é global. A API a fixa em inglês no startup; aqui fazemos o mesmo para
    /// os testes que não passam pelo Program, senão as mensagens dependeriam do idioma da máquina e da ordem de execução.
    /// </summary>
#pragma warning disable CA2255 // Projeto de testes, não uma biblioteca
    [ModuleInitializer]
#pragma warning restore CA2255
    internal static void Initialize() =>
        ValidatorOptions.Global.LanguageManager.Culture = new CultureInfo("en-US");
}
