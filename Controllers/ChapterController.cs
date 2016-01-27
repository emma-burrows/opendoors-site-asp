using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using OpenDoors.Models;
using System.Collections.Specialized;
using System.Web.Configuration;

namespace OpenDoors.Controllers
{
  public class ChapterController : Controller
  {
    private Entities db = new Entities();

    public ActionResult Details(int id = 0)
    {
      NameValueCollection webConfig = (NameValueCollection)WebConfigurationManager.GetSection("ArchiveSettings");
      String key = webConfig["Key"];

      ArchiveConfig config = db.ArchiveConfigs.Where(c => c.Key == key).First();
      TempData["config"] = config;
      Chapter chapter = db.Chapters.Find(id);
      if (chapter == null)
      {
        return HttpNotFound();
      }
      return View(chapter);
    }

    protected override void Dispose(bool disposing)
    {
      db.Dispose();
      base.Dispose(disposing);
    }
  }
}
