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
    private List<String> letters;

    public AuthorController()
    {
      archive = new ArchiveImporter(db);
      config = archive.config;
      letters = db.Authors
                  .Where(a => a.Stories.Count + a.Bookmarks.Count > 0)
                  .GroupBy(p => p.Name.Substring(0, 1))
                  .Select(x => x.Key.ToUpper())
                  .OrderBy(x => x)
                  .ToList();
    }
    //
    // GET: /Author/
    public ActionResult Index(string letter = "", int page = 1, int pageSize = 30)
    {
      setTempData();
      if (String.IsNullOrEmpty(letter)) letter = letters.First();
      if (letter == "_") letter = "\\_";
      String tableName = config.Key + "_authors";
      List<Author> authors = db.Authors
        .SqlQuery("SELECT * FROM " + tableName + " WHERE Name LIKE '" + letter + "%' ORDER BY Name")
        .ToList<Author>()
        .Where(a => a.Stories.Count + a.Bookmarks.Count > 0)
        .ToList<Author>();
      return View(authors.OrderBy(i => i.Name).ToPagedList(page, pageSize));
    }

    public ActionResult Import(int id, string letter = "", int page = 1, int pageSize = 30,
                               ImportSettings.ImportType type = ImportSettings.ImportType.Work)
    {
      setTempData();
      if (String.IsNullOrEmpty(letter)) letter = letters.First();
      TempData["result"] = archive.importMany(new int[] { id }, this.Request.RequestContext, type);
      return RedirectToAction("Index", new { letter = letter, page = page, pageSize = pageSize });
    }

    public ActionResult ImportNone(int id, bool doNotImport, string letter = "", int page = 1, int pageSize = 30)
    {
      setTempData();
      if (String.IsNullOrEmpty(letter)) letter = letters.First();
      var author = db.Authors.Find(id);
      author.DoNotImport = doNotImport;

      db.Entry(author).State = EntityState.Modified;
      db.SaveChanges();
      int[] storyIds = author.Stories.Where(s => s.DoNotImport == !doNotImport).Select(s => s.ID).ToArray();
      int[] bookmarkIds = author.Bookmarks.Where(b => b.DoNotImport == !doNotImport).Select(b => b.ID).ToArray();

      ArchiveResult result = archive.importNone(storyIds, doNotImport, ImportSettings.ImportType.Work);
      ArchiveResult bookmarkResult = archive.importNone(bookmarkIds, doNotImport, ImportSettings.ImportType.Bookmark);
      result.BookmarkResponses = bookmarkResult.BookmarkResponses;
      TempData["result"] = result;
      return RedirectToAction("Index", new { letter = letter, page = page, pageSize = pageSize });
    }

    public ActionResult ImportAll(int id, string letter = "", int page = 1, int pageSize = 30,
                                  ImportSettings.ImportType type = ImportSettings.ImportType.Work)
    {
      setTempData();
      if (String.IsNullOrEmpty(letter)) letter = letters.First();
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
                new List<BookmarkResponse>());
        }
        else {
          TempData["result"] = 
            new ArchiveResult(
                new List<String>() { "All works are already marked as imported" }, 
                new List<StoryResponse>(),
                null);
        }
      } else {
        TempData["result"] = archive.importMany(ids, Request.RequestContext, type);
      }
      return RedirectToAction("Index", new { letter = letter, page = page, pageSize = pageSize });
    }

    public ActionResult CheckAll(int id, string letter = "", int page = 1, int pageSize = 30)
    {
      setTempData();
      if (String.IsNullOrEmpty(letter)) letter = letters.First();
      int[] ids = db.Authors.Find(id).Stories.Select(s => s.ID).ToArray();
      TempData["result"] = archive.checkMany(ids, Request.RequestContext);
      return RedirectToAction("Index", new { letter = letter, page = page, pageSize = pageSize });
    }

    protected override void Dispose(bool disposing)
    {
      db.Dispose();
      base.Dispose(disposing);
    }

    private void setTempData()
    {
      TempData["config"] = config;
      TempData["letters"] = letters;
    }
  }
}
