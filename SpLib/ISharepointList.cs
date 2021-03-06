using SP = Microsoft.SharePoint.Client;

namespace SpLib
{
    interface ISharepointList
    {
        SP.ClientContext Context
        {
            get;
        }

        string Url
        {
            get;
        }
    }
}
