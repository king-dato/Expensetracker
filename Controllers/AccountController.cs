﻿using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Mvc;
using ExpenseTracker.DatabaseContext;
using ExpenseTracker.Models;


namespace ExpenseTracker.Controllers
{
    public class AccountController : Controller
    {
        private readonly AppDbContext _dbContext;

        public AccountController()
        {
            _dbContext = new AppDbContext();
        }

        // GET: /Account/Register
        [HttpGet]
        public ActionResult Register()
        {
            return View();
        }

        // POST: /Account/Register
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Register(RegisterViewModel model)
        {
            if (ModelState.IsValid)
            {
                // Perform additional validation for password and email requirements
                if (!IsValidPassword(model.Password))
                {
                    ModelState.AddModelError("Password", "Password must contain at least 8 characters including at least one uppercase letter, one lowercase letter, and one digit.");
                    return View(model);
                }

                if (!IsValidEmail(model.Email))
                {
                    ModelState.AddModelError("Email", "Invalid email format.");
                    return View(model);
                }

                // Check if the username is already taken
                if (_dbContext.Users.Any(u => u.Username == model.Username))
                {
                    ModelState.AddModelError("Username", "Username already taken.");
                    return View(model);
                }

                // Create a new User instance and map the form data
                var user = new User
                {
                    Username = model.Username,
                    Password = model.Password,
                    Email = model.Email
                };

                // Save the user to the database
                _dbContext.Users.Add(user);
                _dbContext.SaveChanges();

                return RedirectToAction("Login");
            }

            return View(model);
        }

        // GET: /Account/Login
        [HttpGet]
        public ActionResult Login()
        {
            return View();
        }

        // POST: /Account/Login
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Login(LoginViewModel model)
        {
            if (ModelState.IsValid)
            {
                // Retrieve the user from the database based on the provided username
                var user = _dbContext.Users.SingleOrDefault(u => u.Username == model.Username);

                // Verify the user credentials
                if (user != null && user.Password == model.Password)
                {
                    // Set an authentication cookie or session variable to indicate that the user is logged in
                    Session["UserId"] = user.UserId;

                    return RedirectToAction("UserDashboard", "Account");
                }

                // Invalid username or password
                ModelState.AddModelError("", "Invalid username or password");
            }

            return View(model);
        }

        // Password validation method
        private bool IsValidPassword(string password)
        {
            // Use regex pattern to enforce password requirements
            const string pattern = @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).{8,}$";
            return Regex.IsMatch(password, pattern);
        }

        // Email validation method
        private bool IsValidEmail(string email)
        {
            // Use regex pattern to validate email format
            const string pattern = @"^[a-zA-Z0-9_.+-]+@[a-zA-Z0-9-]+\.[a-zA-Z0-9-.]+$";
            return Regex.IsMatch(email, pattern);
        }

        public ActionResult UserDashboard()
        {
            // Retrieve the current user's ID
            var userId = (int)Session["UserId"];

            // Retrieve the current budget for the user
            var currentBudget = _dbContext.Budgets
                .FirstOrDefault(b => b.UserId == userId);

            // Retrieve the top expense day in the last week
            var dateThreshold = DateTime.Now.AddDays(-7);
            var topExpenseDay = _dbContext.Expenses
                .Where(e => e.UserId == userId && e.Date >= dateThreshold)
                .GroupBy(e => DbFunctions.TruncateTime(e.Date))
                .Select(g => new { Date = g.Key, TotalAmount = g.Sum(e => e.Amount) })
                .OrderByDescending(g => g.TotalAmount)
                .FirstOrDefault();

            DateTime? topExpenseDate = topExpenseDay?.Date;

            // Retrieve the most recent expenses
            var recentExpenses = _dbContext.Expenses
                .Where(e => e.UserId == userId)
                .OrderByDescending(e => e.Date)
                .Take(5)
                .ToList();

            // Calculate budget summary
            var totalExpenses = _dbContext.Expenses
                .Where(e => e.UserId == userId && e.BudgetId == currentBudget.BudgetId)
                .Sum(e => e.Amount);
            var remainingBudget = currentBudget.MonthlyIncome - totalExpenses;
            var budgetSpentPercentage = (totalExpenses / currentBudget.MonthlyIncome) * 100;

            var expenseBreakdown = _dbContext.Expenses
                .Where(e => e.UserId == userId && e.BudgetId == currentBudget.BudgetId)
                .GroupBy(e => e.Category)
                .Select(g => new { Label = g.Key, Amount = g.Sum(e => e.Amount) })
                .ToDictionary(e => e.Label, e => e.Amount);

            var viewModel = new UserDashboardViewModel
            {
                CurrentBudget = currentBudget,
                BudgetSummary = new BudgetSummaryViewModel
                {
                    RemainingBudget = remainingBudget,
                    BudgetSpentPercentage = budgetSpentPercentage
                },
                RecentExpenses = recentExpenses,
                TopExpenseDay = topExpenseDate?.ToString("yyyy-MM-dd"),
                RemainingBudget = remainingBudget,
                BudgetSpentPercentage = budgetSpentPercentage,
                ExpenseBreakdown = expenseBreakdown,
                BudgetName = currentBudget.Name,
                TotalExpenses = totalExpenses
            };

            return View(viewModel);
        }

    }
}