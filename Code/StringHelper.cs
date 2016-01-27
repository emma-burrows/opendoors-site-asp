using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace OpenDoors
{
  public class StringHelper
  {
    static public string notLibrary(string s)
    {
      if (s.EndsWith(", The"))
        s = "The " + s.Remove(s.LastIndexOf(", The"), 5);
      if (s.EndsWith(", A"))
        s = "A " + s.Remove(s.LastIndexOf(", A"), 3);

      return s;
    }

  }
}
