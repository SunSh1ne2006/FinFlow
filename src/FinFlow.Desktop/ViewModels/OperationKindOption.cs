namespace FinFlow.Desktop.ViewModels;

/// <summary>Вид операции для выпадающего списка (код домена + русская подпись).</summary>
public class OperationKindOption
{
    public OperationKindOption(string code, string name)
    {
        Code = code;
        Name = name;
    }

    public string Code { get; }
    public string Name { get; }

    public override string ToString() => Name;
}
