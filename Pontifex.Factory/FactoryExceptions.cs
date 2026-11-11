using System;

namespace Pontifex.Factory;

public class FactoryException : Exception
{
    public FactoryException(FactoryError error)
        : base((error ?? throw new ArgumentNullException(nameof(error))).Message)
    {
        Error = error;
    }

    public FactoryException(FactoryError error, Exception? innerException)
        : base((error ?? throw new ArgumentNullException(nameof(error))).Message, innerException)
    {
        Error = error;
    }

    public FactoryError Error { get; }

    protected static FactoryError WithPhase(FactoryError error, FactoryPhase phase)
    {
        if (error is null)
        {
            throw new ArgumentNullException(nameof(error));
        }

        return error with { Phase = phase };
    }
}

public sealed class RegistrationException : FactoryException
{
    public RegistrationException(FactoryError error)
        : base(WithPhase(error, FactoryPhase.Registration))
    {
    }

    public RegistrationException(string code, string message)
        : base(new FactoryError(code, message, FactoryPhase.Registration, null))
    {
    }
}

public sealed class PrinterException : FactoryException
{
    public PrinterException(FactoryError error)
        : base(WithPhase(error, FactoryPhase.Printing))
    {
    }

    public PrinterException(string code, string message)
        : base(new FactoryError(code, message, FactoryPhase.Printing, null))
    {
    }
}

public sealed class CompilerException : FactoryException
{
    public CompilerException(FactoryError error)
        : base(WithPhase(error, FactoryPhase.Compilation))
    {
    }

    public CompilerException(string code, string message)
        : base(new FactoryError(code, message, FactoryPhase.Compilation, null))
    {
    }
}

public sealed class BuildException : FactoryException
{
    public BuildException(FactoryError error)
        : base(WithPhase(error, FactoryPhase.Building))
    {
    }

    public BuildException(string code, string message)
        : base(new FactoryError(code, message, FactoryPhase.Building, null))
    {
    }
}
