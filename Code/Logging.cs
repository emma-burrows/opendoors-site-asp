using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;

namespace OpenDoors.Code
{
  public class Logging
  {
    private readonly string _pathName;

    public Logging(string pathName)
    {
      this._pathName = pathName;
    }

    public void LogToFile(string message)
    {

      StreamWriter sw = new StreamWriter(_pathName, true);
      sw.WriteLine();
      sw.WriteLine(LogTime() + message);
      sw.Flush();
      sw.Close();
    }

    public void Log(HttpResponseBase Response, string s)
    {
      Response.AppendToLog(s);
      LogToFile(s);
    }

    public String Audit(String ipAddress, String message)
    {
      return string.Format("{0} [{1}]: {2}</br>\n", DateTime.Now.ToString("yyyy-MM-dd HH:mm"), ipAddress, message);
    }

    private string LogTime()
    {
      return DateTime.Now.ToString("yyyy-MM-dd") + " " + DateTime.Now.ToLongTimeString().ToString() + " ==> ";
    }
  }
}