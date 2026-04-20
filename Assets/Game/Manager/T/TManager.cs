using System.Collections.Generic;
using DevWorkbench;

public interface ITManager
{

}

internal sealed partial class TManagerData
{
    public string Key;
}

internal sealed partial class TManager : ITManager
{
    private readonly Dictionary<string, TManagerData> _managerDataDict = new();
}
