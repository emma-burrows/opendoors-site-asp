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
    public class ConfigController : Controller
    {
        private Entities db = new Entities();
        

        //
        // GET: /Config/Details/5

        public ActionResult Details(int id = 4)
        {
          NameValueCollection webConfig = (NameValueCollection)WebConfigurationManager.GetSection("ArchiveSettings");
          String key = webConfig["Key"];
          ArchiveConfig archiveconfig = db.ArchiveConfigs.Where(c => c.Key == key).First();
          if (archiveconfig == null)
          {
              return HttpNotFound();
          }
          return View(archiveconfig);
        }

        //
        // GET: /Config/Edit/5

        public ActionResult Edit(int id = 4)
        {
          NameValueCollection webConfig = (NameValueCollection)WebConfigurationManager.GetSection("ArchiveSettings");
          String key = webConfig["Key"];
          ArchiveConfig archiveconfig = db.ArchiveConfigs.Where(c => c.Key == key).First();
          if (archiveconfig == null)
          {
              return HttpNotFound();
          }
          return View(archiveconfig);
        }

        //
        // POST: /Config/Edit/5

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(ArchiveConfig archiveconfig)
        {
            if (ModelState.IsValid)
            {
              try
              {
                db.Entry(archiveconfig).State = EntityState.Modified;
                db.SaveChanges();
                return RedirectToAction("Details");
              }
              catch (Exception e)
              {
                var ex = e.InnerException.Message;
              }
            }
            return View(archiveconfig);
        }

        protected override void Dispose(bool disposing)
        {
            db.Dispose();
            base.Dispose(disposing);
        }
    }
}
