using PagedList;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
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

    public BookmarkController()
    {
      archive = new ArchiveImporter(db);
      config = archive.config;
    }

    public ActionResult Index(int page = 1, int pageSize = 30)
    {
      TempData["config"] = config;
      List<Bookmark> bookmarks = db.Bookmarks.Where(item => !item.Imported && !item.DoNotImport).ToList<Bookmark>();
      return View(bookmarks.OrderBy(item => item.Title).ToPagedList(page, pageSize));
    }

    public ActionResult Imported(int page = 1, int pageSize = 30)
    {
      TempData["config"] = config;
      List<Bookmark> bookmarks = db.Bookmarks.Where(item => item.Imported && !item.DoNotImport).ToList<Bookmark>();
      return View("Index", bookmarks.OrderBy(item => item.Title).ToPagedList(page, pageSize));
    }

    public ActionResult DoNotImport(int page = 1, int pageSize = 30)
    {
      TempData["config"] = config;
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

    public ActionResult Edit(int id, bool imported = false, bool doNotImport = false)
    {
      Bookmark bookmark = db.Bookmarks.Find(id);
      bookmark.Imported = imported;
      bookmark.DoNotImport = doNotImport;
      if (!bookmark.DoNotImport && bookmark.Author.DoNotImport) {
        bookmark.Author.DoNotImport = false;
      }
      if (ModelState.IsValid)
      {
        db.Entry(bookmark).State = EntityState.Modified;
        db.SaveChanges();
      }
      return Redirect(ControllerContext.HttpContext.Request.UrlReferrer.ToString());
    }

    public ActionResult Import(int id, string view = "Index")
    {
      ArchiveImporter archive = new ArchiveImporter(db);
      string baseUrl = Request.Url.GetLeftPart(UriPartial.Authority) + Request.ApplicationPath + "/Chapter/Details/";
      TempData["result"] = archive.importMany(new int[] { id }, Request.RequestContext, ImportSettings.ImportType.Bookmark);
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
