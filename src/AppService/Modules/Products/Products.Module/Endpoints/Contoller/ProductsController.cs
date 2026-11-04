using Asp.Versioning;
using Catalog.Module.Application.DTOs.Products;
using Catalog.Module.Application.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
 


namespace Catalog.Module.Endpoints.Contoller
{
   
    [ApiController]
    [Route("api/v{version:apiVersion}/products")]
    [ApiVersion("1.0")]
    public class ProductsController : ControllerBase
    {
        private readonly IProductService _productService;

        public ProductsController(IProductService productService)
        {
            _productService = productService;
        }

        [HttpPost]
        public async Task<IActionResult> Create(
            CreateProductRequest request,
            CancellationToken cancellationToken)
        {
            var result = await _productService.CreateAsync(
                request,
                cancellationToken);

            if (!result.IsSuccess)
                return BadRequest(new { error = result.Error });

            return CreatedAtAction(
                nameof(GetById),
                new { id = result.Data, version = "1.0" },
                new { id = result.Data });
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(
            Guid id,
            CancellationToken cancellationToken)
        {
            var result = await _productService.GetByIdAsync(
                id,
                cancellationToken);

            if (!result.IsSuccess)
                return result.ErrorType == Application.Common.Results.ServiceErrorType.NotFound
                    ? NotFound(new { error = result.Error })
                    : BadRequest(new { error = result.Error });

            return Ok(result.Data);
        }

        [HttpGet]
        public async Task<IActionResult> GetPaged(
            [FromQuery] ProductFilter filter,
            CancellationToken cancellationToken)
        {
            var result = await _productService.GetPagedAsync(
                filter,
                cancellationToken);

            return Ok(result.Data);
        }

        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(
            Guid id,
            UpdateProductRequest request,
            CancellationToken cancellationToken)
        {
            var result = await _productService.UpdateAsync(
                id,
                request,
                cancellationToken);

            if (!result.IsSuccess)
                return result.ErrorType == Application.Common.Results.ServiceErrorType.NotFound
                    ? NotFound(new { error = result.Error })
                    : BadRequest(new { error = result.Error });

            return NoContent();
        }

        [HttpPatch("{id:guid}/price")]
        public async Task<IActionResult> ChangePrice(
            Guid id,
            ChangeProductPriceRequest request,
            CancellationToken cancellationToken)
        {
            var result = await _productService.ChangePriceAsync(
                id,
                request.Price,
                cancellationToken);

            if (!result.IsSuccess)
                return result.ErrorType == Application.Common.Results.ServiceErrorType.NotFound
                    ? NotFound(new { error = result.Error })
                    : BadRequest(new { error = result.Error });

            return NoContent();
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(
            Guid id,
            CancellationToken cancellationToken)
        {
            var result = await _productService.DeleteAsync(
                id,
                cancellationToken);

            if (!result.IsSuccess)
                return result.ErrorType == Application.Common.Results.ServiceErrorType.NotFound
                    ? NotFound(new { error = result.Error })
                    : BadRequest(new { error = result.Error });

            return NoContent();
        }
    }
}
