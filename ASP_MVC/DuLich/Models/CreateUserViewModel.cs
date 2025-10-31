using Microsoft.AspNetCore.Mvc.Rendering;
using System.Collections.Generic;

namespace DuLich.Models
{
    public class CreateUserViewModel : RegisterModel
    {
        public SelectList? ChiNhanhSelectList { get; set; }
    }
}
