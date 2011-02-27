using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Par2NET.Interfaces
{
    public interface IParLibrary
    {
        ParResult Process(Par2LibraryArguments par2args);
        /*ParResult Create(ref List<string> inputFiles, ref List<string> recoveryFiles);
        ParResult Verify(ref List<string> inputFiles, ref List<string> recoveryFiles);
        ParResult Repair(ref List<string> inputFiles, ref List<string> recoveryFiles);*/
    }
}
