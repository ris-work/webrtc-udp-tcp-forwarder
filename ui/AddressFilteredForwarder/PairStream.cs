﻿
/* Copyright [2019] RISHIKESHAN LAVAKUMAR <github-public [at] ris.fi>
   Has some minor code from .net core, which is extracted from 
   IP of Microsoft Corporation.

   Licensed under the Apache License, Version 2.0 (the "License");
   you may not use this file except in compliance with the License.
   You may obtain a copy of the License at

       http://www.apache.org/licenses/LICENSE-2.0

   Unless required by applicable law or agreed to in writing, software
   distributed under the License is distributed on an "AS IS" BASIS,
   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
   See the License for the specific language governing permissions and
   limitations under the License.
*/
using System;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
#if !NETSTANDARD2_0
using System.Buffers;
#endif

namespace Rishi.PairStream
{
    ///<summary>
    /// The pair class of the module Rishi.PairStream. Binds a StreamWriter and a StreamReader as a stream.
    ///</summary>
    ///<param name="A">ReadableStream</param>
    ///<param name="B">WritableStream</param>
    public class Pair : System.IO.Stream
    {
        private Stream _B;
        private Stream _A;
        ///<summary>
        /// The pair class of the module Rishi.PairStream. Binds a StreamWriter and a StreamReader as a stream.
        ///</summary>
        ///<param name="A">ReadableStream</param>
        ///<param name="B">WritableStream</param>
        public Pair(Stream A, Stream B)
        {
            this._A = A;
            this._B = B;
        }
        public override int Read(byte[] A, int B, int C)
        {
            Console.WriteLine("Read() is called");
            return _A.Read(A, B, C);
        }
        public override void Write(byte[] A, int B, int C)
        {
            //Console.WriteLine(Encoding.Default.GetString(A));
            Console.WriteLine("Write() is called");
            _B.Write(A, B, C);
        }
        
        public override void Flush()
        {
            Console.WriteLine("Pair: Flush called");
            _B.Flush();
        }
        public override void Close()
        {
            _A.Close();
            _B.Close();
        }

        public override bool CanSeek
        {
            get
            {
                return false;
            }
        }
        public override long Position
        {
            set
            {
                throw new NotSupportedException();
            }
            get
            {
                throw new NotSupportedException();
            }
        }
        public override bool CanRead
        {
            get
            {
                return true;
            }
        }
        public override bool CanWrite
        {
            get
            {
                return true;
            }
        }
        public override long Seek(long offset, System.IO.SeekOrigin origin)
        {
            throw new NotSupportedException();
        }
        public override long Length
        {
            get
            {
                throw new NotSupportedException();
            }
        }
        public override void SetLength(long val)
        {
            throw new NotSupportedException();
        }

        ///<summary>
        /// Bind two streams (i.e. SR→SW, SW→SR or read from Stream A and write to Stream B and read from B and write to A).
        ///</summary>
        public static void BindStreams(Stream A, Stream B)
        {
            new Thread(() => A.CopyTo(B)).Start();
            new Thread(() => B.CopyTo(A)).Start();
        }
        ///<summary>
        /// Async bind two streams. Doesn't throw an exception to the thread from which it is being called.
        ///</summary>
        public static void BindStreamsAsync(Stream A, Stream B)
        {
            new Thread(() => A.CopyToAsync(B)).Start();
            new Thread(() => B.CopyToAsync(A)).Start();
        }
        public override async Task<int> ReadAsync(byte[] A, Int32 B, Int32 C, CancellationToken CT)
        {
            Console.WriteLine("Pair: ReadAsync is called.");
            return await _A.ReadAsync(A, B, C, CT);
        }
        public override async ValueTask<int> ReadAsync(Memory<byte> A, CancellationToken CT)
        {
            Console.WriteLine("Pair: ReadAsync is called.");
            return await _A.ReadAsync(A, CT);
        }
        public override async Task WriteAsync(byte[] A, Int32 B, Int32 C, CancellationToken CT)
        {
            Console.WriteLine("Pair: WriteAsync is called.");
            await _B.WriteAsync(A, B, C, CT);
        }
        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> A, CancellationToken CT)
        {
            Console.WriteLine("Pair: WriteAsync is called.");
            await _B.WriteAsync(A, CT);
        }

        public async ValueTask ReadExactlyAsync(Memory<byte> A)
        {
            await _A.ReadExactlyAsync(A);
        }
        public override async Task FlushAsync(CancellationToken CT)
        {
            await _B.FlushAsync(CT);
        }
        public override async Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
        {
            await CopyToAsync(destination, bufferSize, cancellationToken);
            Console.WriteLine("CopyToAsync is called.");
        }

    }
    ///<summary>
    /// A subclass of <c>Rishi.PairStream.pair</c> which keeps track of number of bytes transferred until Reset().
    ///</summary>
    public class statpair : Pair
    {
        private ulong _BR;
        private ulong _BW;
        public statpair(Stream A, Stream B) : base(A, B)
        {
            _BR = 0;
            _BW = 0;
        }
        public ulong BytesRead
        {
            get
            {
                return _BR;
            }
        }
        public ulong BytesWritten
        {
            get
            {
                return _BW;
            }
        }
        public override int Read(byte[] A, int B, int C)
        {
            _BR += Convert.ToUInt64(C);
            return base.Read(A, B, C);
        }
        public override void Write(byte[] A, int B, int C)
        {
            _BW += Convert.ToUInt64(C);
            base.Write(A, B, C);
            return;
        }
        public void ResetStats()
        {
            _BR = 0;
            _BW = 0;
        }
    }

    ///<summary>
    /// A subclass of <c>Rishi.PairStream.statpair</c> which copies to an array of streams.
    ///</summary>
#if !NETSTANDARD2_0
    public class DupStream : statpair
    {
        public DupStream(Stream A, Stream B) : base(A, B) { }
        public async Task CopyToAsyncInternal(Stream[] destinations, Int32 bufferSize, CancellationToken cancellationToken)
        {
            byte[] buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
            try
            {
                while (true)
                {
                    int bytesRead = await ReadAsync(new Memory<byte>(buffer), cancellationToken).ConfigureAwait(false);
                    if (bytesRead == 0) break;
                    foreach (Stream destination in destinations)
                    {
                        await destination.WriteAsync(new ReadOnlyMemory<byte>(buffer, 0, bytesRead), cancellationToken).ConfigureAwait(false);
                    }
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
    }
#endif

}