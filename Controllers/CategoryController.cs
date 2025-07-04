using Microsoft.AspNetCore.Mvc;
using Mimsv2.Models;
using Npgsql;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Mimsv2.Controllers
{
    public class CategoryController : Controller
    {
        private readonly IConfiguration _configuration;
        public CategoryController(IConfiguration configuration)
        {
            _configuration = configuration;
        }
        [HttpGet]
        public async Task<IActionResult> IndexAsync(string selectedCat = null)
        {
            var allCats = new List<string>();
            var items = new List<CategoryModel>();

            // 🔹 First: Get distinct categories (separate connection)
            using (var conn1 = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            {
                await conn1.OpenAsync();
                string catSql = @"SELECT DISTINCT cat FROM tblincidenttype WHERE active = 'Y' ORDER BY cat";

                using var catCmd = new NpgsqlCommand(catSql, conn1);
                using var catReader = await catCmd.ExecuteReaderAsync();
                while (await catReader.ReadAsync())
                {
                    allCats.Add(catReader["cat"].ToString());
                }
            }

            // 🔹 Second: Get filtered incident type records (new connection)
            using (var conn2 = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            {
                await conn2.OpenAsync();
                string itemSql = @"SELECT * FROM tblincidenttype WHERE active = 'Y'";

                if (!string.IsNullOrEmpty(selectedCat))
                {
                    itemSql += " AND cat = @cat";
                }

                itemSql += " ORDER BY cat";

                using var itemCmd = new NpgsqlCommand(itemSql, conn2);

                if (!string.IsNullOrEmpty(selectedCat))
                {
                    itemCmd.Parameters.AddWithValue("@cat", selectedCat);
                }

                using var reader = await itemCmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    items.Add(new CategoryModel
                    {
                        id = Convert.ToInt32(reader["id"]),
                        cat = reader["cat"].ToString(),
                        active = reader["active"].ToString(),
                        subcat1 = reader["subcat1"].ToString(),
                        subcat2 = reader["subcat2"].ToString(),
                        subcat3 = reader["subcat3"].ToString()
                    });
                }
            }

            // ✅ Send to view
            ViewBag.CatList = allCats;
            ViewBag.SelectedCat = selectedCat;

            return View(items);
        }





        //ADD Category
        [HttpGet] // This handles the initial form load
        public IActionResult AddCategory()
        {
            return View(new CategoryModel { active = "Y" }); // Initialize with default active status
        }


        [HttpPost]
        public async Task<IActionResult> AddCategory(CategoryModel model)
        {
            //if (!ModelState.IsValid)
            //{
                // Important: Return the view with errors if validation fails
               // return View(model); // Make sure you have a corresponding view
           // }

            using var conn = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            await conn.OpenAsync();

            // Fixed SQL query (was missing @ before subcat3)
            string sql = @"INSERT INTO tblincidenttype (cat, subcat1, subcat2, subcat3, active)
                   VALUES (@cat, @subcat1, @subcat2, @subcat3, 'Y')";

            try
            {
                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@cat", model.cat ?? "");
                cmd.Parameters.AddWithValue("@subcat1", model.subcat1 ?? "");
                //cmd.Parameters.AddWithValue("@subcat2", model.subcat2 ?? "");
                //cmd.Parameters.AddWithValue("@subcat3", model.subcat3 ?? "");
                cmd.Parameters.AddWithValue("@subcat2", string.IsNullOrWhiteSpace(model.subcat2) ? "n/a" : model.subcat2);
                cmd.Parameters.AddWithValue("@subcat3", string.IsNullOrWhiteSpace(model.subcat3) ? "n/a" : model.subcat3);

                await cmd.ExecuteNonQueryAsync();
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                // Log the error (consider using ILogger)
                ModelState.AddModelError("", "An error occurred while saving: " + ex.Message);
                return View(model);
            }
        }

        //ADD Category



        //EDIT Category HERE
        [HttpGet]
        public async Task<IActionResult> EditCategory(int id)
        {
            var model = new CategoryModel();
            using var conn = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));

            try
            {
                await conn.OpenAsync();
                string sql = "SELECT * FROM tblincidenttype WHERE id = @id";

                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    using var reader = await cmd.ExecuteReaderAsync();

                    if (await reader.ReadAsync())
                    {
                        model.id = id;
                        model.cat = reader["cat"]?.ToString() ?? string.Empty;
                        model.subcat1 = reader["subcat1"]?.ToString() ?? string.Empty;
                        model.subcat2 = reader["subcat2"]?.ToString() ?? string.Empty;
                        model.subcat3 = reader["subcat3"]?.ToString() ?? string.Empty;
                        model.active = reader["active"]?.ToString() ?? string.Empty; ;
                    }
                }
                return View(model);
            }
            catch (Exception ex)
            {
                // Log error here
                return RedirectToAction("Index");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditCategory(CategoryModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            using var conn = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));

            try
            {
                await conn.OpenAsync();
                string sql = @"UPDATE tblincidenttype 
               SET cat = @cat, 
                   subcat1 = @subcat1, 
                   subcat2 = @subcat2, 
                   subcat3 = @subcat3, 
                   active = @active 
               WHERE id = @id";

                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@cat", model.cat ?? string.Empty);
                cmd.Parameters.AddWithValue("@subcat1", model.subcat1 ?? string.Empty);
                cmd.Parameters.AddWithValue("@subcat2", model.subcat2 ?? string.Empty);
                cmd.Parameters.AddWithValue("@subcat3", model.subcat3 ?? string.Empty);
                cmd.Parameters.AddWithValue("@active", model.active ?? "Y");
                cmd.Parameters.AddWithValue("@id", model.id);

                await cmd.ExecuteNonQueryAsync();
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Error updating category: " + ex.Message);
                return View(model);
            }
        }


        //EDIT Category HERE
    }
}
