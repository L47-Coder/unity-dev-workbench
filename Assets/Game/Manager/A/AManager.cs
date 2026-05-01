using System.Collections.Generic;
using DevWorkbench;

public interface IAManager
{

}

internal sealed partial class AManagerData
{
    public string Key;
}

internal sealed partial class AManager : IAManager
{
    private readonly Dictionary<string, AManagerData> _managerDataDict = new();
}
