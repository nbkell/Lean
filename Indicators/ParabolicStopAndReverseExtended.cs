﻿/*
 * QUANTCONNECT.COM - Democratizing Finance, Empowering Individuals.
 * Lean Algorithmic Trading Engine v2.0. Copyright 2014 QuantConnect Corporation.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
*/

using System;
using QLNet;
using QuantConnect.Data.Fundamental;
using QuantConnect.Data.Market;

namespace QuantConnect.Indicators
{
    /// <summary>
    /// Parabolic SAR Extended Indicator 
    /// Based on TA-Lib implementation
    /// </summary>
    public class ParabolicStopAndReverseExtended : BarIndicator, IIndicatorWarmUpPeriodProvider
    {
        private bool _isLong;
        private IBaseDataBar _previousBar;
        private decimal _sar;
        private decimal _extremePoint;
        private decimal _outputSar;
        private decimal _afShort;
        private decimal _afLong;
        private readonly decimal _sarInit; 
        private readonly decimal _offsetOnReverse;  
        private readonly decimal _afInitShort;
        private readonly decimal _afIncrementShort;
        private readonly decimal _afMaxShort;
        private readonly decimal _afInitLong;
        private readonly decimal _afIncrementLong;
        private readonly decimal _afMaxLong;

        /// <summary>
        /// Create a new Parabolic SAR Extended
        /// </summary>
        /// <param name="name">The name of the Parabolic Stop and Reverse Extended indicator</param>
        /// <param name="sarStart">The starting value for the Parabolic Stop and Reverse indicator</param>
        /// <param name="offsetOnReverse">The offset value to be applied on reverse </param>
        /// <param name="afStartShort">The starting acceleration factor for short positions</param>
        /// <param name="afIncrementShort">The increment value for the acceleration factor for short positions</param>
        /// <param name="afMaxShort">The maximum value for the acceleration factor for short positions</param>
        /// <param name="afStartLong">The starting acceleration factor for long positions</param>
        /// <param name="afIncrementLong">The increment value for the acceleration factor for long positions</param>
        /// <param name="afMaxLong">The maximum value for the acceleration factor for long positions</param>
        public ParabolicStopAndReverseExtended(string name, decimal sarStart = 0.0m, decimal offsetOnReverse = 0.0m, 
            decimal afStartShort = 0.02m, decimal afIncrementShort = 0.02m, decimal afMaxShort = 0.2m, 
            decimal afStartLong = 0.02m, decimal afIncrementLong = 0.02m, decimal afMaxLong = 0.2m) : base(name)
        {
            _sarInit = sarStart;
            _offsetOnReverse = offsetOnReverse;  
            _afShort = _afInitShort = afStartShort;
            _afIncrementShort = afIncrementShort;
            _afMaxShort  = afMaxShort; 
            _afLong = _afInitLong = afStartLong;
            _afIncrementLong = afIncrementLong;
            _afMaxLong = afMaxLong; 
        }

        /// <summary>
        /// Create a new Parabolic SAR Extended
        /// </summary>
        /// <param name="sarStart">The starting value for the Parabolic Stop and Reverse indicator</param>
        /// <param name="offsetOnReverse">The offset value to be applied on reverse </param>
        /// <param name="afStartShort">The starting acceleration factor for short positions</param>
        /// <param name="afIncrementShort">The increment value for the acceleration factor for short positions</param>
        /// <param name="afMaxShort">The maximum value for the acceleration factor for short positions</param>
        /// <param name="afStartLong">The starting acceleration factor for long positions</param>
        /// <param name="afIncrementLong">The increment value for the acceleration factor for long positions</param>
        /// <param name="afMaxLong">The maximum value for the acceleration factor for long positions</param>
        public ParabolicStopAndReverseExtended(decimal sarStart = 0.0m, decimal offsetOnReverse = 0.0m, 
            decimal afStartShort = 0.02m, decimal afIncrementShort = 0.02m, decimal afMaxShort = 0.2m, 
            decimal afStartLong = 0.02m, decimal afIncrementLong = 0.02m, decimal afMaxLong = 0.2m) 
            : this($"SAREXT({sarStart},{offsetOnReverse},{afStartShort},{afIncrementShort},{afMaxShort},{afStartLong},{afIncrementLong},{afMaxLong})", 
            sarStart, offsetOnReverse, afStartShort, afIncrementShort, afMaxShort, afStartLong, afIncrementLong, afMaxLong)
        {
        }

        /// <summary>
        /// Gets a flag indicating when this indicator is ready and fully initialized
        /// </summary>
        public override bool IsReady => Samples >= 2;

        /// <summary>
        /// Required period, in data points, for the indicator to be ready and fully initialized.
        /// </summary>
        public int WarmUpPeriod => 2;

        /// <summary>
        /// Resets this indicator to its initial state
        /// </summary>
        public override void Reset()
        {
            _afShort = _afInitShort;
            _afLong = _afInitLong;
            base.Reset();
        }

        /// <summary>
        /// Computes the next value of this indicator from the given state
        /// </summary>
        /// <param name="input">The trade bar input given to the indicator</param>
        /// <returns>A new value for this indicator</returns>
        protected override decimal ComputeNextValue(IBaseDataBar input)
        {
            // On first iteration we can’t produce an SAR value so we save the current bar and return zero
            if (Samples == 1)
            {

                 _previousBar = input;
                // Makes sense to return _sarInit when its non-zero
                if (_sarInit != 0)
                {
                    return _sarInit;
                }
                // Otherwise, return default
                return input.Close; 
            }

            // On second iteration we initiate the position of extreme point SAR
            if (Samples == 2)
            {
                Init(input);
                _previousBar = input; 
            } 

            if (_isLong) 
            {
                HandleLongPosition(input);
            }
            else
            {
                HandleShortPosition(input);
            }
            _previousBar = input; 
            return _outputSar; 
        }

        /// <summary>
        /// Initialize the indicator values 
        /// </summary>
        private void Init(IBaseDataBar currentBar)
        { 
            // initialize starting sar value 
           if (_sarInit > 0)
           {
                _isLong = true;
                _sar = _sarInit;
           }
           else if (_sarInit < 0)
           {
                _isLong = false;
                _sar = Math.Abs(_sarInit); 
           }
           // same set up as standard PSAR when _sarInit = 0 
           else
           {
                _isLong = !HasNegativeDirectionalMovement(currentBar); 
                _sar = _isLong ? _previousBar.Low : _previousBar.High;
           }

            // initialize extreme point 
            _extremePoint = _isLong ? currentBar.High : currentBar.Low; 
        }

        /// <summary>
        /// Returns true if Directional Movement > 0 between today and yesterday's tradebar (false otherwise)
        /// </summary>
        private bool HasNegativeDirectionalMovement(IBaseDataBar currentBar){
            if (currentBar.Low >= _previousBar.Low)
            {
                return false; 
            }
            var highDiff = currentBar.High - _previousBar.High; 
            var lowDiff = _previousBar.Low - currentBar.Low; 
            return highDiff < lowDiff; 
        } 
        
        /// <summary>
        /// Subroutine that adjusts SAR value to be within today and yesterday's high
        /// (resp. low) bar values at the end of long (resp. short) position updates. 
        /// </summary>
        private void AdjustWithinRecentRange(IBaseDataBar currentBar){ 
            if(_isLong)
            { 
                if (_sar > _previousBar.Low)
                    _sar = _previousBar.Low;
                if (_sar > currentBar.Low)
                    _sar = currentBar.Low;
            }
            else
            {
                if (_sar < _previousBar.High)
                    _sar = _previousBar.High;
                if (_sar < currentBar.High)
                    _sar = currentBar.High;
            }
        }

        /// <summary>
        /// Calculate indicator value when the position is long
        /// </summary>
        private void HandleLongPosition(IBaseDataBar currentBar)
        {
            // Switch to short if the low penetrates the SAR value.
            if (currentBar.Low <= _sar)
            {
                // Switch and Overide the SAR with the ep
                _isLong = false;
                _sar = _extremePoint;

                // Make sure the overide SAR is within yesterday's and today's range.
                AdjustWithinRecentRange(currentBar); 
               
                // Output the overide SAR 
                if(_offsetOnReverse != 0.0m)
                    _sar += _sar * _offsetOnReverse;
                _outputSar = -_sar;

                // Adjust af and ep
                _afShort = _afInitShort;
                _extremePoint = currentBar.Low;

                // Calculate the new SAR
                _sar = _sar + _afShort * (_extremePoint - _sar);

                // Make sure the new SAR is within yesterday's and today's range.
                 AdjustWithinRecentRange(currentBar); 
            }
            // No switch
            else
            {
                // Output the SAR (was calculated in the previous iteration) 
                _outputSar = _sar;

                // Adjust af and ep.
                if (currentBar.High > _extremePoint)
                {
                    _extremePoint = currentBar.High;
                    _afLong += _afIncrementLong;
                    _afLong = Math.Min(_afLong, _afMaxLong); 
                }

                // Calculate the new SAR
                _sar = _sar + _afLong * (_extremePoint - _sar);

                // Make sure the new SAR is within yesterday's and today's range.
                AdjustWithinRecentRange(currentBar); 
            }
        }

        /// <summary>
        /// Calculate indicator value when the position is short
        /// </summary>
        private void HandleShortPosition(IBaseDataBar currentBar)
        {
            // Switch to long if the high penetrates the SAR value.
            if (currentBar.High >= _sar)
            {
                // Switch and overide the SAR with the ep
                _isLong = true;
                _sar = _extremePoint;

                // Make sure the overide SAR is within yesterday's and today's range.
                AdjustWithinRecentRange(currentBar); 

                // Output the overide SAR 
                if(_offsetOnReverse != 0.0m)
                {
                    _sar -= _sar * _offsetOnReverse;
                }
                _outputSar = _sar;

                // Adjust af and ep
                _afLong = _afInitLong;
                _extremePoint = currentBar.High;

                // Calculate the new SAR
                _sar = _sar + _afLong * (_extremePoint - _sar);

                // Make sure the new SAR is within yesterday's and today's range.
                AdjustWithinRecentRange(currentBar); 

            }
            //No switch
            else
            {
                // Output the SAR (was calculated in the previous iteration)
                _outputSar = -_sar;

                // Adjust acceleration factor and extreme point  
                if (currentBar.Low < _extremePoint)
                {
                    _extremePoint = currentBar.Low;
                    _afShort += _afIncrementShort;
                    _afShort = Math.Min(_afShort, _afMaxShort); 
                }

                // Calculate the new SAR
                _sar = _sar + _afShort * (_extremePoint - _sar);

                // Make sure the new SAR is within yesterday's and today's range.
                AdjustWithinRecentRange(currentBar); 
            }
        }
    }
}