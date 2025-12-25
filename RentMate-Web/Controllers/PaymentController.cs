using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RentMate.Data;
using RentMate.Models;
using Microsoft.EntityFrameworkCore;
using RentMate.Shared;
using System;
using Microsoft.Extensions.Localization;

[Authorize]
public class PaymentController : Controller
{
    private readonly RentMateContext _context;
    private readonly IStringLocalizer<PaymentController> _localizer;

    public PaymentController(RentMateContext context, IStringLocalizer<PaymentController> localizer)
    {
        _context = context;
        _localizer = localizer;
    }

    [HttpGet]
    public IActionResult Checkout(int rentalId)
    {
        var rental = _context.Rentals.Include(r => r.Item).FirstOrDefault(r => r.Id == rentalId);
        if (rental == null) return NotFound();
        return View(rental);
    }

    [HttpPost]
    public async Task<IActionResult> ProcessPayment(int rentalId, string cardNumber)
    {
        // Simulation of processing (wait 2 seconds)
        await Task.Delay(2000);

        var rental = _context.Rentals.Find(rentalId);
        
        // Mock logic: if the card ends with "00", payment fails
        if (cardNumber.EndsWith("00")) {
            TempData["Error"] = _localizer["Payment rejected. Please try another card."].Value;
            return RedirectToAction("Checkout", new { rentalId });
        }

        var payment = new RentMate.Models.Payment {
            RentalId = rentalId,
            Amount = 100, // Here the price would be calculated
            Status = PaymentStatus.Success,
            TransactionId = Guid.NewGuid().ToString()
        };

        _context.Payments.Add(payment);
        await _context.SaveChangesAsync();

        return View("Success", payment);
    }
}