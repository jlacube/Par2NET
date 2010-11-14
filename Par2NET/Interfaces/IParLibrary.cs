using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Par2NET.Interfaces
{
    public interface IParLibrary
    {
        ParResult Process(List<string> inputFiles, List<string> recoveryFiles, ParAction action);
        /*ParResult Create(ref List<string> inputFiles, ref List<string> recoveryFiles);
        ParResult Verify(ref List<string> inputFiles, ref List<string> recoveryFiles);
        ParResult Repair(ref List<string> inputFiles, ref List<string> recoveryFiles);*/
    }
}
