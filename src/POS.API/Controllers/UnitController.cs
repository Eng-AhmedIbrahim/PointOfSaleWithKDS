using POS.API.Controllers;
using POS.Contract.Dtos.InventoryDtos;
using POS.Core.Entities.Item;
using POS.Core.Services.Contract.InventoryServices;
using Unit = POS.Core.Entities.Item.Unit;

namespace POS.API.Controllers;

[Route("api/units")]
[Route("api/unit")]
public class UnitController : BaseApiController
{
    private readonly IUnitService _unitService;

    public UnitController(IUnitService unitService)
    {
        _unitService = unitService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var units = await _unitService.GetAllUnitsAsync();
        var dtos = units.Select(u => new UnitDto
        {
            Id = u.Id,
            ArabicName = u.ArabicName,
            EnglishName = u.EnglishName,
            Code = u.Code
        }).ToList();
        return Ok(dtos);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var unit = await _unitService.GetUnitByIdAsync(id);
        if (unit == null) return NotFound();
        
        return Ok(new UnitDto
        {
            Id = unit.Id,
            ArabicName = unit.ArabicName,
            EnglishName = unit.EnglishName,
            Code = unit.Code
        });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] UnitDto dto)
    {
        var unit = new Unit
        {
            ArabicName = dto.ArabicName ?? "",
            EnglishName = dto.EnglishName ?? "",
            Code = dto.Code ?? "",
            CreatedAt = DateTime.UtcNow
        };
        await _unitService.CreateUnitAsync(unit);
        return Ok(true);
    }

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] UnitDto dto)
    {
        var unit = await _unitService.GetUnitByIdAsync(dto.Id);
        if (unit == null) return NotFound();

        unit.ArabicName = dto.ArabicName ?? "";
        unit.EnglishName = dto.EnglishName ?? "";
        unit.Code = dto.Code ?? "";
        unit.UpdatedAt = DateTime.UtcNow;

        await _unitService.UpdateUnitAsync(unit);
        return Ok(true);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        await _unitService.DeleteUnitAsync(id);
        return Ok(true);
    }
}
