﻿//
// Copyright 2021 Google LLC
//
// Licensed to the Apache Software Foundation (ASF) under one
// or more contributor license agreements.  See the NOTICE file
// distributed with this work for additional information
// regarding copyright ownership.  The ASF licenses this file
// to you under the Apache License, Version 2.0 (the
// "License"); you may not use this file except in compliance
// with the License.  You may obtain a copy of the License at
// 
//   http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing,
// software distributed under the License is distributed on an
// "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY
// KIND, either express or implied.  See the License for the
// specific language governing permissions and limitations
// under the License.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Google.Solutions.IapDesktop.Application.Util
{
    public sealed class AsyncLock : IDisposable
    {
        private readonly SemaphoreSlim semaphore = new SemaphoreSlim(1);

        public void Dispose()
        {
            this.semaphore.Dispose();
        }

        public async Task<IDisposable> AcquireAsync(CancellationToken token)
        {
            await this.semaphore.WaitAsync(token).ConfigureAwait(false);
            return new Acquisition(() => this.semaphore.Release());
        }

        public IDisposable Acquire()
        {
            this.semaphore.Wait();
            return new Acquisition(() => this.semaphore.Release());
        }

        private class Acquisition : IDisposable
        {
            private readonly Action release;

            public Acquisition(Action release)
            {
                this.release = release;
            }

            public void Dispose()
            {
                this.release();
            }
        }
    }
}
