using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Flow.Launcher.Plugin
{
    public interface IPathSelected : IFeatures
    {
        Task PathSelected(string path, string hotkey);
    }
}
