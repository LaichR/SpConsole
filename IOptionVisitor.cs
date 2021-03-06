using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpConsole
{
    interface IOptionVisitor
    {
        void Visit(FindOptions findOptions);

        void Visit(ExecOptions batchOptions);

        void Visit(DownloadOptions downloadOptions);

        void Visit(UploadOptions uploadOptions);

    }
}


