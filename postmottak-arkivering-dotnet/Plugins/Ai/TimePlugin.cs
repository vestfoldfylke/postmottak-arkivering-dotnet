using System;
using System.ComponentModel;
using Microsoft.SemanticKernel;

namespace postmottak_arkivering_dotnet.Plugins.Ai;

public class TimePlugin
{
    [KernelFunction("get_current_time")]
    [Description("Get the current time as a DateTime object")]
    public DateTime GetCurrentTime()
    {
        return DateTime.Now;
    }
}