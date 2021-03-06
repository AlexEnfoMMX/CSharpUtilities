﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Primes.cs" company="Microsoft">
//   This file is part of Daviburg Utilities.
//
//   Daviburg Utilities is free software: you can redistribute it and/or modify
//   it under the terms of the GNU General Public License as published by
//   the Free Software Foundation, either version 3 of the License, or
//   (at your option) any later version.
//   
//   Daviburg Utilities is distributed in the hope that it will be useful,
//   but WITHOUT ANY WARRANTY; without even the implied warranty of
//   MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
//   GNU General Public License for more details.
//
//   You should have received a copy of the GNU General Public License
//   along with Daviburg Utilities.  If not, see <see href="https://www.gnu.org/licenses"/>.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace Daviburg.Utilities
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    /// <summary>
    /// Helper class to incrementally discover small prime numbers.
    /// </summary>
    /// <remarks>This class is not meant to discover large prime numbers.</remarks>
    public class Primes
    {
        private static readonly Lazy<Primes> PrimeInstance = new Lazy<Primes>(() => new Primes());
        private long[] primes = { 2, 3, 5, 7, 11, 13 };

        public static Primes Singleton => PrimeInstance.Value;

        /// <summary>
        /// Prevents construction of instances by others, enforcing singleton pattern so prime numbers are only computed once.
        /// </summary>
        private Primes()
        {
        }

        /// <summary>
        /// Determines if an odd value is a prime number by trying to divide it by all the primes we found so far.
        /// </summary>
        /// <param name="value">Value to be tested as prime number.</param>
        /// <remarks>This is known as the Trial division method for checking if a number is prime.</remarks>
        private bool IsPrime(long value)
        {
            // If we haven't found a prime factor lower or equal to the square root, then there won't be a higher prime factor as this factor would need another lower factor to be multiplied by to result in our candidate.
            var maxTest = value.IntegralPartOfSquareRoot();

            // Although our array of prime numbers is zero-based, because we only evaluate odd numbers they will never have the first primer number two as prime factor.
            int existingPrimeIndex = 1;
            while (this.primes[existingPrimeIndex] <= maxTest)
            {
                if (value % this.primes[existingPrimeIndex] == 0)
                {
                    return false;
                }

                existingPrimeIndex++;
            }

            return true;
        }

        /// <summary>
        /// Given the last greatest prime number discovered so far, find the next prime number.
        /// </summary>
        /// <param name="lastPrime">The last greatest prime number discovered so far.</param>
        private long FindNextPrime(long lastPrime)
        {
            // Skip by two - prime numbers are never even but for the first prime value 2,
            // and we've primed our internal array well beyond that.
            long candidate = lastPrime + 2;
            while (!this.IsPrime(candidate))
            {
                candidate += 2;
            }

            return candidate;
        }

        public long this[int index]
        {
            get
            {
                if (index < this.primes.Length)
                {
                    return this.primes[index];
                }

                var oldMax = this.primes.Length;
                Array.Resize(ref this.primes, index + 1);
                for (int indexForNewPrime = oldMax; indexForNewPrime <= index; indexForNewPrime++)
                {
                    this.primes[indexForNewPrime] = this.FindNextPrime(this.primes[indexForNewPrime - 1]);
                }

                return this.primes[index];
            }
        }

        public int DiscoveredPrimesCount => this.primes.Length;

        public long LargestDiscoveredPrime => this.primes[this.primes.Length - 1];

        public Array CheckRangeForPrimes(long rangeStart, long rangeEnd)
        {
            // This is a simple version which only skips even numbers and not factors of 3.
            // Through empyrical testing, the added complexity of skipping factors of 3 does not pay off.
            // Probably because the overhead of IsPrime method is minimal when it returns early.
            var primesFound = new List<long>();
            long candidate = rangeStart;
            do
            {
                if (this.IsPrime(candidate))
                {
                    primesFound.Add(candidate);
                }
            } while ((candidate += 2) <= rangeEnd);

            return primesFound.ToArray();
        }

        public void ParallelRangePrimeCompute(int searchSizeLimit, int chunkSizeLimit)
        {
            var largestFoundPrime = this.primes[this.primes.Length - 1];
            var upperSearchBoundary = Math.Min(largestFoundPrime + searchSizeLimit, Convert.ToInt64(Math.Pow(largestFoundPrime, 2)));
            var tasks = new Queue<Task<Array>>();
            var lowerSearchBoundary = largestFoundPrime + 2;
            long rangeEnd;

            do
            {
                rangeEnd = Math.Min(lowerSearchBoundary + chunkSizeLimit, upperSearchBoundary);
                tasks.Enqueue(
                    Task.Factory
                        .StartNew(
                            (range) =>
                            {
                                return this.CheckRangeForPrimes(((Tuple<long, long>)range).Item1, ((Tuple<long, long>)range).Item2);
                            },
                            new Tuple<long, long>(lowerSearchBoundary, rangeEnd)));
                lowerSearchBoundary = rangeEnd + 2;
            }
            while (rangeEnd != upperSearchBoundary);

            while (tasks.Count != 0)
            {
                var task = tasks.Dequeue();
                var oldMax = this.primes.Length;
                Array.Resize(ref this.primes, this.primes.Length + task.Result.Length);
                Array.Copy(task.Result, 0, this.primes, oldMax, task.Result.Length);
            }
        }

        public void SieveSearch(int searchSizeLimit)
        {
            // Look only at odd numbers, halving the sieve size
            var isFactor = new bool[searchSizeLimit / 2];

            // Special case 1 is not a prime number
            isFactor[0] = true;

            // Stop the sieve search once we have flagged all the factors of primes up to square root of the sieve size.
            // This is for the same reason as the test limit for IsPrime method.
            var maxTest = searchSizeLimit.IntegralPartOfSquareRoot();

            for(var factorPrimeIndex = 1; this.primes[factorPrimeIndex] <= maxTest; factorPrimeIndex++)
            { 
                var prime = this.primes[factorPrimeIndex];

                // For this prime, flag all the factors. These numbers aren't prime.
                // Jump to prime ^ 2 as the first factor to flag as everything below has already gone through the sieve
                for (var factor = prime * prime; factor <= searchSizeLimit; factor += prime * 2)
                {
                    // As we halved our sieve size, adjust the index
                    isFactor[factor >> 1] = true;
                }

                var completedSieveLimit = prime * prime >> 1;
                var newPrimes = new List<long>();

                // All the numbers not-flagged as factors yet and lower than the power of the current prime we used as factor,
                // these are prime numbers. Index limit is adjusted based on halving the sieve.
                // Skip all numbers less than the power of the previous primed used as factor as we already picked new primes
                // from that part of the array.
                for (var index = this.primes[factorPrimeIndex - 1] >> 1; index < completedSieveLimit; index++)
                {
                    if (!isFactor[index])
                    {
                        var discoveredPrime = (index * 2) + 1;
                        if (discoveredPrime > this.LargestDiscoveredPrime)
                        {
                            newPrimes.Add(discoveredPrime);
                        }
                    }
                }

                var oldMax = this.primes.Length;
                Array.Resize(ref this.primes, this.primes.Length + newPrimes.Count);
                Array.Copy(newPrimes.ToArray(), 0, this.primes, oldMax, newPrimes.Count);
            }
        }

        public long LargestPrimeFactorOf(long value)
        {
            var ceiling = value.IntegralPartOfSquareRoot();
            var primeIndex = 0;
            var quotient = value;

            do
            {
                while (quotient % this[primeIndex] != 0)
                {
                    primeIndex++;
                }

                quotient = quotient / this[primeIndex];
            }
            while (this[primeIndex] <= ceiling && quotient >= this.primes[primeIndex]);

            return this[primeIndex];
        }

        /// <summary>
        /// Finds the smallest number that is a multiple of all the positive integral numbers up to the specified value
        /// </summary>
        /// <param name="value">The upper limit of integers which must all be quotients of the result.</param>
        public long SmallestMultipleOfAllNumbersTo(int value)
        {
            // All the prime numbers that are less than the square root of our upper limit value may result in non prime quotients if dividing our result
            // so that all the numbers are quotients of the result. Greater prime numbers are never needed to match non prime quotients,
            // so we will only need them at 'power 1'.
            var ceiling = value.IntegralPartOfSquareRoot();
            var multiple = (long)1;

            // All the prime numbers that are less or equal to our upper limit value must be quotients of our result,
            // so this is the upper limit of our iteration.
            for (var index = 0; this[index] <= value; index++)
            {
                if (this[index] <= ceiling)
                {
                    // We need the prime number to match as many non prime quotients of our result value as possible.
                    // Hence prime power x is less than value
                    // Hence x times log10 prime is less than log10 value
                    // Hence x (integer) is floor of log10 value divided by log10 prime
                    multiple *= Convert.ToInt64(Math.Pow(this[index], Math.Floor(Math.Log10(value) / Math.Log10(this[index]))));
                }
                else
                {
                    // 'power 1' - this prime is too large to 'form' other non-prime quotients less or equal to value
                    multiple *= this[index];
                }
            }

            return multiple;
        }
    }
}
