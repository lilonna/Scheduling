using Newtonsoft.Json;
using NuGet.Packaging;
using Microsoft.EntityFrameworkCore;
using System.Data;

using Scheduling.Models;
using hu_utils;


namespace Scheduling.Utilities
{
    public class UserManagement
    {
        private readonly SchedulingContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public UserManagement(SchedulingContext context, IHttpContextAccessor httpContextAccessor)
        {
            _context = context;

            _httpContextAccessor = httpContextAccessor;
        }
        private void SetSessionInt32(string key, int value)
        {
            _httpContextAccessor.HttpContext.Session.SetInt32(key, value);
        }

        private void SetSessionString(string key, string value)
        {
            _httpContextAccessor.HttpContext.Session.SetString(key, value);
        }

        public void setUserBasicInfo(User user)
        {
            SetSessionString("LastLogon", user.LastLogon.ToString());
            SetSessionInt32("UserId", user.Id);
            SetSessionString("FullName", user.FirstName);
            SetSessionString("Employee", user.FirstName);
            SetSessionString("AmharicFullName", user.FirstName);
            SetSessionString("UserName", user.UserName);
            SetSessionInt32("ActionBy", user.Id);
            //SetSessionInt32("LanguageId", user.DefaultLanguageId);
        }

        public void LoadUserRole(int UserId)
        {
            var userRoles = _context.UserRoles
                .Include(x => x.User)
                .Include(x => x.Role)
                .Select(s => new {
                    s.Id,
                    roleName = s.Role.Name,
                    s.IsDefault

                });
            string roleName = userRoles.Any(x => x.IsDefault) ? userRoles.FirstOrDefault(x => x.IsDefault).roleName : userRoles.Any() ? userRoles.FirstOrDefault().roleName : "";
            SetSessionString("RoleName", roleName);

        }
        //public void populateLanguage()
        //{
        //    var localLanguages = _context.Languages
        //        .Where(m => m.IsActive == true)
        //        .Select(s => new
        //        {
        //            s.Id,
        //            s.Name,
        //            s.LangudgeCode,
        //            s.IsActive,
        //            s.IsDeleted
        //        });
        //   SetSessionString("languages", JsonConvert.SerializeObject(localLanguages));
        //}

        //public void resetApplication(int id)
        //{
        //    _httpContextAccessor.HttpContext.Session.Remove("MySideMenu");
        //    _httpContextAccessor.HttpContext.Session.Remove("menus");
        //    _httpContextAccessor.HttpContext.Session.Remove("menuCategories");
        //    _httpContextAccessor.HttpContext.Session.Remove("MyTopMenu");

        //    SetSessionInt32("MenuTypeId", 1);
        //    SetSessionString("MenuType", "General Menu");
        //    SetSessionString("SelectedItem", "");
        //    SetSessionInt32("ApplicationId", id);
        //    loadMenus(id);
        //}

        //public void loadApplications(int UserId)
        //{

        //    var userRoles = _context.UserRoles
        //        .Include(x => x.User)
        //        .Include(x => x.Role)
        //        .Where(x => x.UserId == UserId && x.IsActive==true && !x.IsDeleted).Select(s => new { 
        //            s.Id,
        //            s.RoleId,
        //            s.UserId,
        //            roleName=s.Role.Name,
        //            s.IsDefault
        //        }).ToList();

        //    var apps = new List<Application>();
        //    if (userRoles.Any(x=>x.roleName== "Super Administrator"))
        //    {
        //        var allApplications = _context.Applications
        //       .Where(x => x.IsActive == true && !x.IsDeleted)
        //       .ToList();

        //        SetSessionString("RoleSuper", "Super Administrator");
        //        if (allApplications.Any())
        //        {
        //            apps.AddRange(allApplications);
        //            SetSessionString("Applications", JsonConvert.SerializeObject(apps));
        //            loadMenus(apps.FirstOrDefault()?.Id ?? 0);
        //        }
        //    }
        //    else
        //    {
        //        var roleApplicationId = _context.RoleApplications
        //      .Where(x => userRoles.Select(ur => ur.RoleId).Contains(x.RoleId))
        //      .Select(s => s.Id)
        //      .ToList();
        //        var userApplications = _context.Applications
        //      .Where(x => roleApplicationId.Contains(x.Id));
        //        if (userApplications.Any())
        //        {
        //            apps.AddRange(userApplications);
        //            SetSessionString("Applications", JsonConvert.SerializeObject(apps));
        //            loadMenus(userApplications.FirstOrDefault().Id);
        //        }
        //    }
        //}

        //public void loadMenus(int ApplicationId)
        //{
        //    int userId = (int)_httpContextAccessor.HttpContext.Session.GetInt32("UserId");
        //    List<UserRole> roles = new List<UserRole>();
        //    HashSet<int> roleIds = new HashSet<int>();
        //    List<MenuDisplay> menus = new List<MenuDisplay>();
        //    List<CategoryDisplay> menuCategories = new List<CategoryDisplay>();
        //    List<Priviledges> userPrivileges = new List<Priviledges>();
        //    Application application = _context.Applications.FirstOrDefault(x=>x.Id==ApplicationId);

        //    var userRoles = _context.UserRoles
        //        .Include(x => x.Role)
        //        .Where(x => x.UserId == userId && x.IsActive == true && !x.IsDeleted).Select(s => new {
        //            s.Id,
        //            s.RoleId,
        //            roleName=s.Role.Name,
        //            s.IsDefault
        //        })
        //        .AsNoTracking()
        //        .ToList();

        //    bool isSuperAdmin = userRoles.Any(x => x.roleName == "Super Administrator");

        //   IList<UserPrivileges>  privileges = new List<UserPrivileges>();

        //    if (isSuperAdmin)
        //    {
        //        var superPrivileges = _context.Menus
        //       .Include(x => x.MenuCategory)
        //       .Include(x => x.RolesMenus)
        //       .Where(m => m.IsActive == true)
        //       .Select(s => new UserPrivileges
        //       {
        //           Id = s.Id,
        //           Controller = s.Controller,
        //           Action = s.Action,
        //           Title = s.Title,
        //           CategoryId = s.MenuCategoryId,
        //           IsMenu = s.IsMenu,
        //           ApplicationId = s.MenuCategory.ApplicationId,
        //           MenuTypeId = s.MenuCategory.MenuTypeId,
        //           PrimaryController = s.MenuCategory.MenuType.PrimaryController,
        //           MenuCategory = s.MenuCategory
        //       });
        //        privileges.AddRange(superPrivileges);
        //    }
        //    else
        //    {
        //        int[] userRoleIds = userRoles.Select(x => x.Id).ToArray();
        //        var superPrivileges = _context.Menus
        //                .Include(x => x.MenuCategory)
        //                .Include(x => x.RolesMenus)
        //                .ThenInclude(x => x.Role).ThenInclude(x => x.UserRoles)
        //                .Where(m => m.RolesMenus.Any(a => userRoleIds.Contains(a.RoleId)) && m.IsActive == true)
        //                .ToList()
        //                .Select(s => new UserPrivileges
        //                {
        //                    Id = s.Id,
        //                    Controller = s.Controller,
        //                    Action = s.Action,
        //                    Title = s.Title,
        //                    CategoryId = s.MenuCategoryId,
        //                    IsMenu = s.IsMenu,
        //                    ApplicationId = s.MenuCategory.ApplicationId,
        //                    MenuTypeId = s.MenuCategory.MenuTypeId,
        //                    PrimaryController = s.MenuCategory.MenuType.PrimaryController,
        //                    MenuCategory = s.MenuCategory
        //                });
        //        privileges.AddRange(superPrivileges);
        //    }
        //    menus = privileges
        //        .Where(m => m.ApplicationId == ApplicationId && m.IsMenu)
        //        .Select(x => new MenuDisplay
        //        {
        //            Id = x.Id,
        //            Title = x.Title,
        //            Controller = x.Controller,
        //            Action = x.Action,
        //            MenuCategoryId = x.CategoryId
        //        })
        //        .Distinct()
        //        .ToList();

        //    menuCategories = privileges
        //        .Where(m => m.ApplicationId == ApplicationId && m.IsMenu)
        //        .Select(x => x.MenuCategory)
        //        .Distinct()
        //        .Select(x => new CategoryDisplay
        //        {
        //            Id = x.Id,
        //            Name = x.Name,
        //            Icon = x.Icon,
        //            OrderNumber = x.OrderNumber,
        //            MenuTypeId = x.MenuTypeId
        //        })
        //        .Distinct()
        //        .ToList();

        //    userPrivileges = privileges
        //        .Select(m => new Priviledges
        //        {
        //            id = m.Id,
        //            controller = m.Controller.ToUpper(),
        //            action = m.Action.ToUpper(),
        //            menuTypeId = m.MenuTypeId,
        //            primaryController = m.PrimaryController,
        //            applicationId = (int)m.ApplicationId
        //        })
        //        .Distinct()
        //        .ToList();

        //    SetSessionString("Previleges", JsonConvert.SerializeObject(userPrivileges));
        //    SetSessionString("Role", JsonConvert.SerializeObject(userRoles));
        //    SetSessionString("menus", JsonConvert.SerializeObject(menus));
        //    SetSessionString("menuCategories", JsonConvert.SerializeObject(menuCategories));
        //    SetSessionString("ApplicationName", application.Acronym);
        //    SetSessionString("ApplicationCode", application.Code);
        //}

        public void saveUserLogons(User user, string fingerPrint, string browser, string platform, string timeZone, string userAgent)
        {
            //if (!user.UserLogons.Any(x => x.FingerPrint == fingerPrint))
            //{
            //    var verCode = Password._get_password_reset_code(8);
            //    UserLogon nwLogon = new UserLogon
            //    {
            //        UserId = user.Id,
            //        FingerPrint = fingerPrint,
            //        Browser = browser,
            //        Platform = platform,
            //        TimeZone = timeZone,
            //        UserAgent = userAgent,
            //        VerificationCode = verCode,
            //        IsVerified = true,
            //        IsActive = true,
            //        IsDeleted = false,
            //        LogDate = DateTime.Now
            //    };
            //    _context.Add(nwLogon);
            //    _context.SaveChanges();
            //}
        }
    }
}


