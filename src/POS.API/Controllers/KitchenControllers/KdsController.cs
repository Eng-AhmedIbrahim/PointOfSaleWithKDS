using Microsoft.AspNetCore.Mvc;
using POS.Contract.Dtos.KitchenDtos;
using POS.Core.Services.Contract.KitchenServices;

namespace POS.API.Controllers.KitchenControllers;

[Route("api/[controller]")]
[ApiController]
public class KdsController : BaseApiController
{
    private readonly IKdsService _kdsService;

    public KdsController(IKdsService kdsService)
    {
        _kdsService = kdsService;
    }

    [HttpGet("screens")]
    public async Task<ActionResult<List<KdsScreenDto>>> GetScreens([FromQuery] int branchId = 0)
    {
        var screens = await _kdsService.GetScreensAsync(branchId);
        return Ok(screens);
    }

    [HttpGet("screens/{id}")]
    public async Task<ActionResult<KdsScreenDto>> GetScreenById(int id)
    {
        var screen = await _kdsService.GetScreenByIdAsync(id);
        if (screen == null) return NotFound();
        return Ok(screen);
    }

    [HttpPost("screens")]
    public async Task<ActionResult<KdsScreenDto>> SaveScreen([FromBody] KdsScreenDto dto)
    {
        var saved = await _kdsService.SaveScreenAsync(dto);
        return Ok(saved);
    }

    [HttpDelete("screens/{id}")]
    public async Task<ActionResult<bool>> DeleteScreen(int id)
    {
        var result = await _kdsService.DeleteScreenAsync(id);
        return Ok(result);
    }

    [HttpGet("active-kitchen")]
    public async Task<ActionResult<List<KdsOrderTicketDto>>> GetActiveKitchen([FromQuery] int branchId = 0, [FromQuery] int screenId = 0)
    {
        var orders = await _kdsService.GetActiveKitchenOrdersAsync(branchId, screenId);
        return Ok(orders);
    }

    [HttpPost("bump-kitchen/{orderId}")]
    public async Task<ActionResult<bool>> BumpKitchen(int orderId)
    {
        var result = await _kdsService.BumpKitchenOrderAsync(orderId);
        return Ok(result);
    }

    [HttpPost("recall-kitchen/{orderId}")]
    public async Task<ActionResult<bool>> RecallKitchen(int orderId)
    {
        var result = await _kdsService.RecallKitchenOrderAsync(orderId);
        return Ok(result);
    }

    [HttpGet("active-pickup")]
    public async Task<ActionResult<List<KdsOrderTicketDto>>> GetActivePickup([FromQuery] int branchId = 0)
    {
        var orders = await _kdsService.GetActivePickupOrdersAsync(branchId);
        return Ok(orders);
    }

    [HttpPost("complete-pickup/{orderId}")]
    public async Task<ActionResult<bool>> CompletePickup(int orderId)
    {
        var result = await _kdsService.CompletePickupOrderAsync(orderId);
        return Ok(result);
    }
}
