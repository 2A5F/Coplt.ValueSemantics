namespace Coplt.ValueSemantics.Analysis.Utilities;

public readonly record struct NameWrap(string Code)
{
    public override string ToString() => Code;
}
