using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace DuLich.Models
{
    public class TourBookedViewModel
    {
        public MyTourItem TourBooked { get; set; }
        public int BookingId { get; set; }
        public bool HideCancelButton { get; set; }
    }
}
