// Copyright (c) 2015 SharpYaml - Alexandre Mutel
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
// 
// -------------------------------------------------------------------------------
// SharpYaml is a fork of YamlDotNet https://github.com/aaubry/YamlDotNet
// published with the following license:
// -------------------------------------------------------------------------------
// 
// Copyright (c) 2008, 2009, 2010, 2011, 2012 Antoine Aubry
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy of
// this software and associated documentation files (the "Software"), to deal in
// the Software without restriction, including without limitation the rights to
// use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies
// of the Software, and to permit persons to whom the Software is furnished to do
// so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SharpYaml.Tests
{
        [TestClass]
    public class LookAheadBufferTests
    {
        private const string TestString = "abcdefghi";
        private const int Capacity = 4;

        [TestMethod]
        public void ShouldHaveReadOnceWhenPeekingAtOffsetZero()
        {
            var reader = CreateFakeReader(TestString);
            var buffer = CreateBuffer(reader, Capacity);

            Assert.AreEqual('a', buffer.Peek(0));
        }

        [TestMethod]
        public void ShouldHaveReadTwiceWhenPeekingAtOffsetOne()
        {
            var reader = CreateFakeReader(TestString);
            var buffer = CreateBuffer(reader, Capacity);

            buffer.Peek(0);

            Assert.AreEqual('b', buffer.Peek(1));
        }

        [TestMethod]
        public void ShouldHaveReadThriceWhenPeekingAtOffsetTwo()
        {
            var reader = CreateFakeReader(TestString);
            var buffer = CreateBuffer(reader, Capacity);

            buffer.Peek(0);
            buffer.Peek(1);

            Assert.AreEqual('c', buffer.Peek(2));
        }

        [TestMethod]
        public void ShouldNotHaveReadAfterSkippingOneCharacter()
        {
            var reader = CreateFakeReader(TestString);
            var buffer = CreateBuffer(reader, Capacity);

            buffer.Peek(2);

            buffer.Skip(1);

            Assert.AreEqual('b', buffer.Peek(0));
            Assert.AreEqual('c', buffer.Peek(1));
        }

        [TestMethod]
        public void ShouldHaveReadOnceAfterSkippingOneCharacter()
        {
            var reader = CreateFakeReader(TestString);
            var buffer = CreateBuffer(reader, Capacity);

            buffer.Peek(2);

            buffer.Skip(1);

            Assert.AreEqual('d', buffer.Peek(2));
        }

        [TestMethod]
        public void ShouldHaveReadTwiceAfterSkippingOneCharacter()
        {
            var reader = CreateFakeReader(TestString);
            var buffer = CreateBuffer(reader, Capacity);

            buffer.Peek(2);

            buffer.Skip(1);

            Assert.AreEqual('e', buffer.Peek(3));
        }

        [TestMethod]
        public void ShouldHaveReadOnceAfterSkippingFiveCharacters()
        {
            var reader = CreateFakeReader(TestString);
            var buffer = CreateBuffer(reader, Capacity);

            buffer.Peek(2);
            buffer.Skip(1);
            buffer.Peek(3);

            buffer.Skip(4);

            Assert.AreEqual('f', buffer.Peek(0));
        }

        [TestMethod]
        public void ShouldHaveReadOnceAfterSkippingSixCharacters()
        {
            var reader = CreateFakeReader(TestString);
            var buffer = CreateBuffer(reader, Capacity);

            buffer.Peek(2);
            buffer.Skip(1);
            buffer.Peek(3);
            buffer.Skip(4);
            buffer.Peek(0);

            buffer.Skip(1);

            Assert.AreEqual('g', buffer.Peek(0));
        }

        [TestMethod]
        public void ShouldHaveReadOnceAfterSkippingSevenCharacters()
        {
            var reader = CreateFakeReader(TestString);
            var buffer = CreateBuffer(reader, Capacity);

            buffer.Peek(2);
            buffer.Skip(1);
            buffer.Peek(3);
            buffer.Skip(4);
            buffer.Peek(1);

            buffer.Skip(2);

            Assert.AreEqual('h', buffer.Peek(0));
        }

        [TestMethod]
        public void ShouldHaveReadOnceAfterSkippingEightCharacters()
        {
            var reader = CreateFakeReader(TestString);
            var buffer = CreateBuffer(reader, Capacity);

            buffer.Peek(2);
            buffer.Skip(1);
            buffer.Peek(3);
            buffer.Skip(4);
            buffer.Peek(2);

            buffer.Skip(3);

            Assert.AreEqual('i', buffer.Peek(0));
        }

        [TestMethod]
        public void ShouldHaveReadOnceAfterSkippingNineCharacters()
        {
            var reader = CreateFakeReader(TestString);
            var buffer = CreateBuffer(reader, Capacity);

            buffer.Peek(2);
            buffer.Skip(1);
            buffer.Peek(3);
            buffer.Skip(4);
            buffer.Peek(3);

            buffer.Skip(4);

            Assert.AreEqual('\0', buffer.Peek(0));
        }

        [TestMethod]
        public void ShouldFindEndOfInput()
        {
            var reader = CreateFakeReader(TestString);
            var buffer = CreateBuffer(reader, Capacity);

            buffer.Peek(2);
            buffer.Skip(1);
            buffer.Peek(3);
            buffer.Skip(4);
            buffer.Peek(3);
            buffer.Skip(4);
            buffer.Peek(0);

            Assert.IsTrue(buffer.EndOfInput);
        }

        [TestMethod]
        public void ShouldThrowWhenPeekingBeyondCapacity()
        {
            var reader = CreateFakeReader(TestString);
            var buffer = CreateBuffer(reader, Capacity);

            Assert.ThrowsException<ArgumentOutOfRangeException>(() => buffer.Peek(4));
        }

        [TestMethod]
        public void ShouldThrowWhenSkippingBeyondCurrentBuffer()
        {
            var reader = CreateFakeReader(TestString);
            var buffer = CreateBuffer(reader, Capacity);

            buffer.Peek(3);

            Assert.ThrowsException<ArgumentOutOfRangeException>(() => buffer.Skip(5));
        }

        private static TextReader CreateFakeReader(string text)
        {
            return new StringReader(text);
        }

        private static LookAheadBuffer CreateBuffer(TextReader reader, int capacity)
        {
            return new LookAheadBuffer(reader, capacity);
        }
    }
}



