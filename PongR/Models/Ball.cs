﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace PongR.Models
{
    public class Ball
    {
        public int Radius { get; set; }
        public Point Coordinates { get; set; }
        public string Direction { get; set; }
        public double Angle { get; set; }
        public int FixedStep { get; set; }
    }
}