using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RentMate.Data;
using RentMate.Models;
using Microsoft.EntityFrameworkCore;
using RentMate.Shared;
using System;

[Authorize]
public class PaymentController : Controller
{
    private readonly RentMateContext _context;

    public PaymentController(RentMateContext context) => _context = context;

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
        // Simulacija procesiranja (počakamo 2 sekundi)
        await Task.Delay(2000);

        var rental = _context.Rentals.Find(rentalId);
        
        // Mock logika: če se kartica konča z "00", plačilo spodleti
        if (cardNumber.EndsWith("00")) {
            TempData["Error"] = "Plačilo zavrnjeno. Poskusite z drugo kartico.";
            return RedirectToAction("Checkout", new { rentalId });
        }

        var payment = new RentMate.Models.Payment {
            RentalId = rentalId,
            Amount = 100, // Tukaj bi izračunal ceno
            Status = PaymentStatus.Success,
            TransactionId = Guid.NewGuid().ToString()
        };

        _context.Payments.Add(payment);
        await _context.SaveChangesAsync();

        return View("Success", payment);
    }
}