using Asp.Versioning;
using Catalog.Module.Application.Common.Results;
using Catalog.Module.Application.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Products.Module.Application.DTOs.Categories;

 
namespace Catalog.Module.Endpoints.Contoller
{
   
    [ApiController]
    [ApiVersion(1.0)]
    [Route("api/v{version:apiVersion}/categories")]

    public class CategoriesController : ControllerBase
    {
        private readonly ICategoryService _categoryService;

        public CategoriesController(ICategoryService categoryService)
        {
            _categoryService = categoryService;
        }

        [HttpPost]
        public async Task<IActionResult> Create(
            [FromBody] CreateCategoryRequest request,
            CancellationToken cancellationToken)
        {
            var result = await _categoryService.CreateAsync(
                request, cancellationToken);

            if (!result.IsSuccess)
                return HandleFailure(result);

            return CreatedAtAction(
                nameof(GetById),
                new { id = result.Data!.Id },
                result.Data);
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(
            Guid id,
            CancellationToken cancellationToken)
        {
            var result = await _categoryService.GetByIdAsync(
                id, cancellationToken);

            if (!result.IsSuccess)
                return HandleFailure(result);

            return Ok(result.Data);
        }

        [HttpGet]
        public async Task<IActionResult> GetAll(
            CancellationToken cancellationToken)
        {
            var result = await _categoryService.GetAllAsync(
                cancellationToken);

            if (!result.IsSuccess)
                return HandleFailure(result);

            return Ok(result.Data);
        }

        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(
            Guid id,
            [FromBody] UpdateCategoryRequest request,
            CancellationToken cancellationToken)
        {
            var result = await _categoryService.UpdateAsync(
                id, request, cancellationToken);

            if (!result.IsSuccess)
                return HandleFailure(result);

            return Ok(result.Data);
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(
            Guid id,
            CancellationToken cancellationToken)
        {
            var result = await _categoryService.DeleteAsync(
                id, cancellationToken);

            if (!result.IsSuccess)
                return HandleFailure(result);

            return NoContent();
        }

        private IActionResult HandleFailure(ServiceResult result)
        {
            return result.ErrorType switch
            {
                ServiceErrorType.NotFound => NotFound(result),
                ServiceErrorType.Conflict => Conflict(result),
                ServiceErrorType.Validation => BadRequest(result),
                _ => StatusCode(500, result)
            };
        }
    }
}
