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

namespace OpenDoors.Controllers
{
  public class StoryController : Controller
  {
    private Entities db = new Entities();
    private ArchiveImporter archive;
    private ArchiveConfig config;

    public StoryController()
    {
      archive = new ArchiveImporter(db);
      config = archive.config;
    }

    public ActionResult Index(int page = 1, int pageSize = 30)
    {
      TempData["config"] = config;
      List<Story> stories = db.Stories.Where(item => !item.Imported && !item.DoNotImport).ToList<Story>();
      return View(stories.OrderBy(item => item.Title).ToPagedList(page, pageSize));
    }

    public ActionResult Imported(int page = 1, int pageSize = 30)
    {
      TempData["config"] = config;
      List<Story> stories = db.Stories.Where(item => item.Imported && !item.DoNotImport).ToList<Story>();
      return View("Index", stories.OrderBy(item => item.Title).ToPagedList(page, pageSize));
    }

    public ActionResult DoNotImport(int page = 1, int pageSize = 30)
    {
      TempData["config"] = config;
      List<Story> stories = db.Stories.Where(item => item.DoNotImport).ToList<Story>();
      return View("Index", stories.OrderBy(item => item.Title).ToPagedList(page, pageSize));
    }

    public ActionResult Details(int id = 0)
    {
      TempData["config"] = config;
      Story story = db.Stories.Find(id);
      if (story == null)
      {
        return HttpNotFound();
      }
      return View(story);
    }

    public ActionResult Edit(int id, bool imported = false, bool doNotImport = false)
    {
      Story story = db.Stories.Find(id);
      story.Imported = imported;
      story.DoNotImport = doNotImport;
      if (!story.DoNotImport && story.Author.DoNotImport) {
        story.Author.DoNotImport = false;
      }
      if (ModelState.IsValid)
      {
        db.Entry(story).State = EntityState.Modified;
        db.SaveChanges();
      }
      return Redirect(ControllerContext.HttpContext.Request.UrlReferrer.ToString());
    }

    public ActionResult Import(int id, string view = "Index")
    {
      ArchiveImporter archive = new ArchiveImporter(db);
      string baseUrl = Request.Url.GetLeftPart(UriPartial.Authority) + Request.ApplicationPath + "/Chapter/Details/";
      TempData["result"] = archive.importMany(new int[] { id }, Request.RequestContext);
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
