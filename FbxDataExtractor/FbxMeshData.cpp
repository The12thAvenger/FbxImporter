#include "pch.h"
#include <msclr/marshal.h>
#include <vector>

#include "FbxMeshData.h"
#include "Util.h"

namespace FbxDataExtractor {
    FbxMeshData::FbxMeshData(const char* name)
    {
        Name = gcnew String(name);
        VertexIndices = gcnew List<int>();
        VertexData = gcnew List<FbxVertexData^>();
    }

    List<FbxMeshData^>^ FbxMeshData::Import(String^ path)
    {
        FbxManager* fbxManager = FbxManager::Create();

        FbxIOSettings* ios = FbxIOSettings::Create(fbxManager, IOSROOT);
        fbxManager->SetIOSettings(ios);
        FbxImporter* importer = FbxImporter::Create(fbxManager, "");

        msclr::interop::marshal_context^ context = gcnew msclr::interop::marshal_context();
        const char* pathPtr = context->marshal_as<const char*>(path);

        importer->Initialize(pathPtr, -1, ios);

        delete context;

        FbxScene* scene = FbxScene::Create(fbxManager, "");
        importer->Import(scene);
        importer->Destroy();
        ios->Destroy();

        List<FbxMeshData^>^ meshList = gcnew List<FbxMeshData^>();
        for (int i = 0; i < scene->GetNodeCount(); i++)
        {
            FbxMesh* mesh = scene->GetNode(i)->GetMesh();
            if (mesh == nullptr) {
                continue;
            }
            meshList->Add(Import(mesh));
        }

        fbxManager->Destroy();

        return meshList;
    }
    
    FbxMeshData^ FbxMeshData::Import(FbxMesh* fbxMesh)
    {
        FbxMeshData^ meshData = gcnew FbxMeshData(fbxMesh->GetNode()->GetName());

        if (!fbxMesh->IsTriangleMesh()) {
            throw gcnew IO::InvalidDataException(gcnew String("Fbx mesh \"" + meshData->Name + "\" is not triangulated. Please triangulate all meshes."));
        }

        fbxMesh->GenerateNormals();
        fbxMesh->GenerateTangentsDataForAllUVSets();

        const int* vertexIndices = fbxMesh->GetPolygonVertices();
        for (int i = 0; i < fbxMesh->GetPolygonVertexCount(); i++)
        {
            meshData->VertexIndices->Add(vertexIndices[i]);
        }

        const int numControlPoints = fbxMesh->GetControlPointsCount();
        const std::vector<FbxVector4> normalsPerVertex = GetElementByVertex(fbxMesh->GetElementNormal(), vertexIndices, numControlPoints);

        std::vector<std::vector<FbxVector4>> tangentPerVertexLayers{ };
        const int numTangents = fbxMesh->GetElementTangentCount();
        for (int i = 0; i < numTangents; i++)
        {
            tangentPerVertexLayers.push_back(GetElementByVertex(fbxMesh->GetElementTangent(i), vertexIndices, numControlPoints));
        }

        std::vector<std::vector<FbxVector2>> uvPerVertexLayers{ };
        const int numUvs = fbxMesh->GetElementUVCount();
        for (int i = 0; i < numUvs; i++)
        {
            uvPerVertexLayers.push_back(GetElementByVertex(fbxMesh->GetElementUV(i), vertexIndices, numControlPoints));
        }
        
        
        for (int i = 0; i < numControlPoints; i++)
        {
            if (!meshData->VertexIndices->Contains(i)) {
                meshData->VertexData->Add(nullptr);
                continue;
            }

            FbxVertexData^ vertexData = gcnew FbxVertexData();

            FbxVector4 position = fbxMesh->GetControlPointAt(i);
            vertexData->Position = FbxVectorToArray<3>(position);

            FbxVector4 normal = normalsPerVertex.at(i);
            vertexData->Normal = FbxVectorToArray<3>(normal);
            
            for (int j = 0; j < numTangents; j++)
            {
                FbxVector4 tangent = tangentPerVertexLayers[j][i];
                vertexData->Tangents->Add(FbxVectorToArray<4>(tangent));
            }

            for (int j = 0; j < numUvs; j++)
            {
                FbxVector2 uv = uvPerVertexLayers[j][i];
                vertexData->UVs->Add(FbxVectorToArray<3>(uv));
            }

            meshData->VertexData->Add(vertexData);
        }
        
        FbxSkin* skin = nullptr;
        for (int i = 0; i < fbxMesh->GetDeformerCount(); i++)
        {
            if (fbxMesh->GetDeformer(i)->GetDeformerType() == FbxDeformer::eSkin) {
                skin = static_cast<FbxSkin*>(fbxMesh->GetDeformer(i));
                break;
            }
        }

        if (skin != nullptr) {
            for (int i = 0; i < skin->GetClusterCount(); i++)
            {
                FbxCluster* cluster = skin->GetCluster(i);
                const int* controlPointIndices = cluster->GetControlPointIndices();
                const double* controlPointWeights = cluster->GetControlPointWeights();
                for (int j = 0; j < cluster->GetControlPointIndicesCount(); j++)
                {
                    FbxVertexData^ vertexData = meshData->VertexData[controlPointIndices[j]];
                    if (vertexData == nullptr) {
                        continue;
                    }

                    String^ boneName = gcnew String(cluster->GetLink()->GetName());
                    vertexData->BoneNames->Add(boneName);

                    const float boneWeight = controlPointWeights[j];
                    vertexData->BoneWeights->Add(boneWeight);
                }
            }
        }

        Predicate<FbxVertexData^>^ nullCondition = gcnew Predicate<FbxVertexData^>(VertexDataIsNull);
        meshData->VertexData->RemoveAll(nullCondition);

        return meshData;
    }

    bool FbxMeshData::VertexDataIsNull(FbxVertexData^ vertexData)
    {
        return vertexData == nullptr;
    }
}

