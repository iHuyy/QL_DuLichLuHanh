using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DuLich.Models.Data;

namespace DuLich.Controllers.Api
{
    [ApiController]
    [Route("api/image")]
    public class ImageController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public ImageController(ApplicationDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Lấy ảnh từ BLOB theo ID
        /// GET api/image/{id}
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetImage(int id)
        {
            var anhTour = await _context.AnhTours.FindAsync(id);
            
            if (anhTour == null || anhTour.DuLieuAnh == null)
            {
                return NotFound();
            }

            // Trả về ảnh với content-type phù hợp
            var contentType = anhTour.LoaiAnh ?? "image/jpeg";
            return File(anhTour.DuLieuAnh, contentType);
        }

        /// <summary>
        /// Lấy nhiều ảnh của một tour
        /// GET api/image/tour/{tourId}
        /// </summary>
        [HttpGet("tour/{tourId}")]
        public async Task<IActionResult> GetTourImages(int tourId)
        {
            var images = await _context.AnhTours
                .Where(a => a.MaTour == tourId)
                .OrderBy(a => a.MaAnh)
                .ToListAsync();

            if (!images.Any())
            {
                return NotFound();
            }

            // Trả về thông tin ảnh (ID, tên file, loại)
            var result = images.Select(img => new
            {
                id = img.MaAnh,
                url = $"/api/image/{img.MaAnh}",
                contentType = img.LoaiAnh,
                description = img.MoTa,
                uploadDate = img.NgayTaiLen
            }).ToList();

            return Ok(result);
        }
    }
}
