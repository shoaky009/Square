namespace Square.Images.Webp.Vp8;

internal static partial class Vp8Decoder
{
    private sealed partial class Decoder
    {
        private void PredictLarge(int row, int column, int size, int mode)
        {
            if (mode == PredDc)
            {
                var sum = size;
                for (var i = 0; i < size; i++) sum += R(row - 1, column + i) + R(row + i, column - 1);
                Fill(row, column, size, (byte)(sum / (2 * size)));
            }
            else if (mode == PredTm)
            {
                var corner = R(row - 1, column - 1);
                for (var y = 0; y < size; y++) for (var x = 0; x < size; x++)
                    W(row + y, column + x, Clip8(R(row + y, column - 1) + R(row - 1, column + x) - corner));
            }
            else if (mode == PredVe)
            {
                for (var y = 0; y < size; y++) for (var x = 0; x < size; x++) W(row + y, column + x, R(row - 1, column + x));
            }
            else if (mode == PredHe)
            {
                for (var y = 0; y < size; y++) for (var x = 0; x < size; x++) W(row + y, column + x, R(row + y, column - 1));
            }
            else if (mode == 10)
            {
                var sum = size / 2;
                for (var y = 0; y < size; y++) sum += R(row + y, column - 1);
                Fill(row, column, size, (byte)(sum / size));
            }
            else if (mode == 11)
            {
                var sum = size / 2;
                for (var x = 0; x < size; x++) sum += R(row - 1, column + x);
                Fill(row, column, size, (byte)(sum / size));
            }
            else Fill(row, column, size, 128);
        }

        private void Predict4(int row, int column, int mode)
        {
            Span<int> t = stackalloc int[8];
            Span<int> l = stackalloc int[4];
            var corner = R(row - 1, column - 1);
            for (var i = 0; i < 8; i++) t[i] = R(row - 1, column + i);
            for (var i = 0; i < 4; i++) l[i] = R(row + i, column - 1);
            static int A2(int a, int b) => (a + b + 1) >> 1;
            static int A3(int a, int b, int c) => (a + 2 * b + c + 2) >> 2;
            if (mode == PredDc)
            {
                var sum = 4; for (var i = 0; i < 4; i++) sum += t[i] + l[i];
                Fill(row, column, 4, (byte)(sum >> 3)); return;
            }
            if (mode == PredTm)
            {
                for (var y = 0; y < 4; y++) for (var x = 0; x < 4; x++) W(row + y, column + x, Clip8(l[y] + t[x] - corner)); return;
            }
            if (mode == PredVe)
            {
                for (var x = 0; x < 4; x++) { var v = A3(x == 0 ? corner : t[x - 1], t[x], t[x + 1]); for (var y = 0; y < 4; y++) W(row + y, column + x, v); } return;
            }
            if (mode == PredHe)
            {
                for (var y = 0; y < 4; y++) { var v = A3(y == 0 ? corner : l[y - 1], l[y], y == 3 ? l[3] : l[y + 1]); for (var x = 0; x < 4; x++) W(row + y, column + x, v); } return;
            }
            if (mode == PredRd)
            {
                Span<int> v = stackalloc int[7];
                v[0]=A3(l[3],l[2],l[1]); v[1]=A3(l[2],l[1],l[0]); v[2]=A3(l[1],l[0],corner);
                v[3]=A3(l[0],corner,t[0]); v[4]=A3(corner,t[0],t[1]); v[5]=A3(t[0],t[1],t[2]); v[6]=A3(t[1],t[2],t[3]);
                for(var y=0;y<4;y++) for(var x=0;x<4;x++) W(row+y,column+x,v[3+x-y]); return;
            }
            if (mode == PredVr)
            {
                var ab=A2(corner,t[0]); var bc=A2(t[0],t[1]); var cd=A2(t[1],t[2]); var de=A2(t[2],t[3]);
                var rqp=A3(l[2],l[1],l[0]); var qpa=A3(l[1],l[0],corner); var pab=A3(l[0],corner,t[0]);
                var abc=A3(corner,t[0],t[1]); var bcd=A3(t[0],t[1],t[2]); var cde=A3(t[1],t[2],t[3]);
                int[] values=[ab,bc,cd,de,pab,abc,bcd,cde,qpa,ab,bc,cd,rqp,pab,abc,bcd];
                for(var y=0;y<4;y++) for(var x=0;x<4;x++) W(row+y,column+x,values[y*4+x]);
                return;
            }
            if (mode == PredLd)
            {
                for(var y=0;y<4;y++) for(var x=0;x<4;x++) { var i=x+y; W(row+y,column+x,A3(t[i],t[i+1],t[Math.Min(i+2,7)])); } return;
            }
            if (mode == PredVl)
            {
                var ab=A2(t[0],t[1]); var bc=A2(t[1],t[2]); var cd=A2(t[2],t[3]); var de=A2(t[3],t[4]);
                var abc=A3(t[0],t[1],t[2]); var bcd=A3(t[1],t[2],t[3]); var cde=A3(t[2],t[3],t[4]);
                var def=A3(t[3],t[4],t[5]); var efg=A3(t[4],t[5],t[6]); var fgh=A3(t[5],t[6],t[7]);
                int[] values=[ab,bc,cd,de,abc,bcd,cde,def,bc,cd,de,efg,bcd,cde,def,fgh];
                for(var y=0;y<4;y++) for(var x=0;x<4;x++) W(row+y,column+x,values[y*4+x]); return;
            }
            if (mode == PredHd)
            {
                var sr=A2(l[3],l[2]); var rq=A2(l[2],l[1]); var qp=A2(l[1],l[0]); var pa=A2(l[0],corner);
                var srq=A3(l[3],l[2],l[1]); var rqp=A3(l[2],l[1],l[0]); var qpa=A3(l[1],l[0],corner);
                var pab=A3(l[0],corner,t[0]); var abc=A3(corner,t[0],t[1]); var bcd=A3(t[0],t[1],t[2]);
                int[] values=[pa,pab,abc,bcd,qp,qpa,pa,pab,rq,rqp,qp,qpa,sr,srq,rq,rqp];
                for(var y=0;y<4;y++) for(var x=0;x<4;x++) W(row+y,column+x,values[y*4+x]);
                return;
            }
            for (var y = 0; y < 4; y++) for (var x = 0; x < 4; x++)
            {
                var i = y + (x >> 1);
                W(row + y, column + x, i >= 3 ? l[3] : (x & 1) == 0 ? A2(l[i], l[i + 1]) : A3(l[i], l[i + 1], l[Math.Min(i + 2, 3)]));
            }
        }

        private void Fill(int row, int column, int size, byte value)
        {
            for (var y = 0; y < size; y++) work.AsSpan((row + y) * 32 + column, size).Fill(value);
        }
    }
}
