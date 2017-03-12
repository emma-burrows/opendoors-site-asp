using PagedList;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using OpenDoors.Code;
using OpenDoors.Models;
using OtwArchive.Models.Request;

namespace OpenDoors.Controllers
{
  public class BookmarkController : Controller
  {
    private Entities db = new Entities();
    private ArchiveImporter archive;
    private ArchiveConfig config;
    private Logging logger;

    public BookmarkController()
    {
      logger = new Logging(Path.Combine(HttpRuntime.AppDomainAppPath + "\\Log" + DateTime.Today.ToString("-yyyy-MM-dd") + ".log"));
      archive = new ArchiveImporter(db);
      config = archive.config;
    }

    public ActionResult Index(int page = 1, int pageSize = 30)
    {
      TempData["config"] = config;
      TempData["type"] = "Bookmark";
      List<Bookmark> bookmarks = db.Bookmarks.Where(item => !item.Imported && !item.DoNotImport).ToList<Bookmark>();
      return View(bookmarks.OrderBy(item => item.Title).ToPagedList(page, pageSize));
    }

    public ActionResult Imported(int page = 1, int pageSize = 30)
    {
      TempData["config"] = config;
      TempData["type"] = "Bookmark";
      List<Bookmark> bookmarks = db.Bookmarks.Where(item => item.Imported && !item.DoNotImport).ToList<Bookmark>();
      return View("Index", bookmarks.OrderBy(item => item.Title).ToPagedList(page, pageSize));
    }

    public ActionResult DoNotImport(int page = 1, int pageSize = 30)
    {
      TempData["config"] = config;
      TempData["type"] = "Bookmark";
      List<Bookmark> bookmarks = db.Bookmarks.Where(item => item.DoNotImport).ToList<Bookmark>();
      return View("Index", bookmarks.OrderBy(item => item.Title).ToPagedList(page, pageSize));
    }

    public ActionResult Details(int id = 0)
    {
      TempData["config"] = config;
      Bookmark bookmark = db.Bookmarks.Find(id);
      if (bookmark == null)
      {
        return HttpNotFound();
      }
      return View(bookmark);
    }

    public ActionResult Edit(int id, String ipAddress, bool imported = false, bool doNotImport = false, bool brokenLink = false)
    {
      Bookmark bookmark = db.Bookmarks.Find(id);
      bookmark.Imported = imported;
      bookmark.DoNotImport = doNotImport;
      bookmark.BrokenLink = brokenLink;
      
      // Set author not DNI if this is toggled to import
      if (!bookmark.DoNotImport && bookmark.Author.DoNotImport) {
        bookmark.Author.DoNotImport = false;
      }

      // Set author to DNI if all its items are DNI
      if (bookmark.DoNotImport && bookmark.Author.Stories.All(s => s.DoNotImport))
      {
        bookmark.Author.DoNotImport = true;
      }

      // Set message
      String message = string.Format("[{0}]: Set imported={1}, doNotImport={2}, brokenLink={3} all for bookmark {4} [{5}]",
        Request.UserHostAddress, imported, doNotImport, brokenLink, bookmark.Title, id);
      bookmark.ImportNotes += logger.Audit(ipAddress, string.Format("Set imported={0}, doNotImport={1}, brokenLink={2}", imported, doNotImport, brokenLink));

      if (ModelState.IsValid)
      {
        db.Entry(bookmark).State = EntityState.Modified;
        db.Entry(bookmark.Author).State = EntityState.Modified;
        db.SaveChanges();
      }

      logger.Log(Response, config.Name + " " + message); 

      return Redirect(ControllerContext.HttpContext.Request.UrlReferrer.ToString());
    }

    public ActionResult Import(int id, string view = "Index")
    {
      ArchiveImporter archive = new ArchiveImporter(db);
      string baseUrl = Request.Url.GetLeftPart(UriPartial.Authority) + Request.ApplicationPath + "/Chapter/Details/";
      var result = archive.ImportMany(new int[] { id }, Request.RequestContext, Request.UserHostAddress, null, ImportSettings.ImportType.Bookmark);

      TempData["result"] = result;
      logger.Log(Response, string.Format("{0} [{1}]: Import bookmark id [{2}] - response: {3}",
  config.Name, Request.UserHostAddress, id, String.Concat("\n", result.ConcatAllMessages("\n"))));

      if (view == "Details")
      {
        return RedirectToAction(view, new { id = id, ViewData = ViewData });
      }
      else
        return RedirectToAction(view, ViewData);
    }

    protected override void Dispose(bool disposing)
    {
      db.Dispose();
      base.Dispose(disposing);
    }

  }
}
