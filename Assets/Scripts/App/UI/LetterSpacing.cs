using UnityEngine;
using UnityEngine.UI;

namespace GravityHalfDead
{
    public sealed class LetterSpacing : BaseMeshEffect
    {
        public float spacing;

        public override void ModifyMesh(VertexHelper vertexHelper)
        {
            if (!IsActive() || spacing == 0 || vertexHelper.currentVertCount == 0)
                return;

            var vertex = new UIVertex();
            var characterCount = vertexHelper.currentVertCount / 4;
            for (var character = 0; character < characterCount; character++)
            {
                var offset = spacing * character;
                for (var corner = 0; corner < 4; corner++)
                {
                    var index = character * 4 + corner;
                    vertexHelper.PopulateUIVertex(ref vertex, index);
                    vertex.position += Vector3.right * offset;
                    vertexHelper.SetUIVertex(vertex, index);
                }
            }
        }
    }
}
