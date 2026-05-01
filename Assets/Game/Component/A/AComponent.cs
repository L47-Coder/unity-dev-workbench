using DevWorkbench;

public sealed partial class AComponentData
{
    [TableColumn(Header = "123")]
    public int a;
    public string Key;
}

public sealed partial class AComponent
{
    private readonly AComponentData _componentData;
}
