using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static OpenDDZ.Utils.Logger;

namespace OpenDDZ.Utils
{
    internal class Recorder
    {
        private Recorder()
        {


        }
        public static Recorder Instance { get; private set; } = new Recorder();
        internal void Record(string json,string fileName="")
        {
            lock (this)
            {   
                if(fileName=="")
                    fileName = $"record_{DateTime.Now:yyyyMMdd_HHmmss}.json";
            }
            var recordDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "records");
            if (!System.IO.Directory.Exists(recordDir))
            {
                System.IO.Directory.CreateDirectory(recordDir);
            }
            fileName = System.IO.Path.Combine(recordDir, fileName);
            System.IO.File.WriteAllText(fileName, json);

        }
    }
}
