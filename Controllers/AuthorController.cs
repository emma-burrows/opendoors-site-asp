using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using OpenDoors.Models;
using PagedList;
using OtwArchive;
using OpenDoors.Code;
using OtwArchive.Models.Request;

namespace OpenDoors.Controllers
{
  public class AuthorController : Controller
  {
    private Entities db = new Entities();
    private ArchiveImporter archive;
    private ArchiveConfig config;

    public AuthorController()
    {
      archive = new ArchiveImporter(db);
      config = archive.config;
    }
    //
    // GET: /Author/

    public ActionResult Index(string letter = "A", int page = 1, int pageSize = 30)
    {
      TempData["config"] = config;
      String tableName = config.Key + "_authors";
      TempData["letters"] = db.Authors
                                .GroupBy(p => p.Name.Substring(0, 1))
                                .Select(x => x.Key.ToUpper())
                                .OrderBy(x => x)
                                .ToList();
      List<Author> authors = db.Authors
        .SqlQuery("SELECT * FROM " + tableName + " WHERE Name LIKE '" + letter + "%' ORDER BY Name")
        .ToList<Author>()
        .Where(a => a.Stories.Count + a.Bookmarks.Count > 0)
        .ToList<Author>();
      return View(authors.OrderBy(i => i.Name).ToPagedList(page, pageSize));
    }

    public ActionResult Import(int id, string letter = "A", int page = 1, int pageSize = 30,
                               ImportSettings.ImportType type = ImportSettings.ImportType.Work)
    {
      TempData["config"] = config;
      TempData["letters"] = db.Authors
                                .GroupBy(p => p.Name.Substring(0, 1))
                                .Select(x => x.Key.ToUpper())
                                .OrderBy(x => x)
                                .ToList();
      TempData["result"] = archive.importMany(new int[] { id }, this.Request.RequestContext, type);
      return RedirectToAction("Index", new { letter = letter, page = page, pageSize = pageSize });
    }

    public ActionResult ImportNone(int id, bool doNotImport, string letter = "A", int page = 1, int pageSize = 30)
    {
      TempData["config"] = config;
      TempData["letters"] = db.Authors
                          .GroupBy(p => p.Name.Substring(0, 1))
                          .Select(x => x.Key.ToUpper())
                          .OrderBy(x => x)
                          .ToList();
      var author = db.Authors.Find(id);
      author.DoNotImport = doNotImport;
      foreach (Story s in db.Authors.Find(id).Stories.Where(s => s.DoNotImport == false))
      {
        s.DoNotImport = doNotImport;
      }
      foreach (Bookmark s in db.Authors.Find(id).Bookmarks.Where(s => s.DoNotImport == false))
      {
        s.DoNotImport = doNotImport;
      }

      db.Entry(author).State = EntityState.Modified;
      db.SaveChanges();
      int[] ids = author.Stories.Where(s => s.DoNotImport == !doNotImport).Select(s => s.ID).ToArray();
      TempData["result"] = archive.importNone(ids, doNotImport);
      return RedirectToAction("Index", new { letter = letter, page = page, pageSize = pageSize });
    }

    public ActionResult ImportAll(int id, string letter = "A", int page = 1, int pageSize = 30,
                                  ImportSettings.ImportType type = ImportSettings.ImportType.Work)
    {
      TempData["config"] = config;
      TempData["letters"] = db.Authors
                          .GroupBy(p => p.Name.Substring(0, 1))
                          .Select(x => x.Key.ToUpper())
                          .OrderBy(x => x)
                          .ToList();
      int[] ids = new int[] {};
      if (type == ImportSettings.ImportType.Work)
      {
        ids = db.Authors.Find(id).Stories.Where(s => s.Imported == false).Select(s => s.ID).ToArray();
      }
      else if (type == ImportSettings.ImportType.Bookmark)
      {
        ids = db.Authors.Find(id).Bookmarks.Where(b => b.Imported == false).Select(b => b.ID).ToArray();
      }
      if (ids.Count() == 0) {
        if (type == ImportSettings.ImportType.Bookmark) {
          TempData["result"] =
            new ArchiveResult(
                new List<String>() { "All bookmarks are already marked as imported" },
                null,
                new Dictionary<int, BookmarkResponse>());
        }
        else {
          TempData["result"] = 
            new ArchiveResult(
                new List<String>() { "All works are already marked as imported" }, 
                new Dictionary<int,StoryResponse>(),
                null);
        }
      } else {
        TempData["result"] = archive.importMany(ids, Request.RequestContext, type);
      }
      return RedirectToAction("Index", new { letter = letter, page = page, pageSize = pageSize });
    }

    public ActionResult CheckAll(int id, string letter = "A", int page = 1, int pageSize = 30)
    {
      TempData["config"] = config;
      TempData["letters"] = db.Authors
                          .GroupBy(p => p.Name.Substring(0, 1))
                          .Select(x => x.Key.ToUpper())
                          .OrderBy(x => x)
                          .ToList();
      int[] ids = db.Authors.Find(id).Stories.Select(s => s.ID).ToArray();
      TempData["result"] = archive.checkMany(ids, Request.RequestContext);
      return RedirectToAction("Index", new { letter = letter, page = page, pageSize = pageSize });
    }

    protected override void Dispose(bool disposing)
    {
      db.Dispose();
      base.Dispose(disposing);
    }
  }
}
