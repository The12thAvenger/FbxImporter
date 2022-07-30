#include <fbxsdk.h>
#include <msclr/marshal.h>
#include <vector>

#include "pch.h"
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
        FbxMeshData^ meshData = gcnew FbxMeshData(fbxMesh->GetName());

        if (!fbxMesh->IsTriangleMesh()) {
            throw gcnew System::IO::InvalidDataException(gcnew String("Mesh named " + meshData->Name + " is not triangulated. Please triangulate all meshes."));
        }

        fbxMesh->GenerateNormals();
        fbxMesh->GenerateTangentsDataForAllUVSets();

        int* vertexIndices = fbxMesh->GetPolygonVertices();
        for (int i = 0; i < fbxMesh->GetPolygonVertexCount(); i++)
        {
            meshData->VertexIndices->Add(vertexIndices[i]);
        }

        FbxGeometryElementNormal* normalElement = fbxMesh->GetElementNormal();
        normalElement->RemapIndexTo(FbxLayerElement::EMappingMode::eByControlPoint);
        if (normalElement->GetMappingMode() != FbxLayerElement::EMappingMode::eByControlPoint) {
            throw gcnew Exception("Remap failed");
        }
        FbxLayerElementArrayTemplate<FbxVector4> normals = normalElement->GetDirectArray();

        std::vector<FbxLayerElementArrayTemplate<FbxVector4>> tangentLayers{ };
        std::vector<FbxLayerElementArrayTemplate<FbxVector4>> uvLayers{ };
        for (int i = 0; i < fbxMesh->GetLayerCount(); i++)
        {
            FbxLayer* layer = fbxMesh->GetLayer(i);

            FbxLayerElementTangent* tangentElement = (FbxLayerElementTangent*)layer->GetLayerElementOfType(FbxLayerElement::EType::eTangent);
            tangentElement->RemapIndexTo(FbxLayerElement::EMappingMode::eByControlPoint);
            if (tangentElement->GetMappingMode() != FbxLayerElement::EMappingMode::eByControlPoint) {
                throw gcnew Exception("Remap failed");
            }
            tangentLayers.push_back(tangentElement->GetDirectArray());

            FbxLayerElementTangent* UVElement = (FbxLayerElementTangent*)layer->GetLayerElementOfType(FbxLayerElement::EType::eUV);
            UVElement->RemapIndexTo(FbxLayerElement::EMappingMode::eByControlPoint);
            if (UVElement->GetMappingMode() != FbxLayerElement::EMappingMode::eByControlPoint) {
                throw gcnew Exception("Remap failed");
            }
            uvLayers.push_back(UVElement->GetDirectArray());
        }

        for (int i = 0; i < fbxMesh->GetControlPointsCount(); i++)
        {
            if (!meshData->VertexIndices->Contains(i)) {
                meshData->VertexData->Add(nullptr);
                continue;
            }

            FbxVertexData^ vertexData = gcnew FbxVertexData();

            FbxVector4 position = fbxMesh->GetControlPointAt(i);
            Util::FbxVectorToArray(position, vertexData->Position);

            FbxVector4 normal = normals[i];
            Util::FbxVectorToArray(normal, vertexData->Normal);

            for (int j = 0; j < fbxMesh->GetLayerCount(); j++)
            {
                vertexData->Tangents[j] = gcnew array<float>(4);
                vertexData->UVs[j] = gcnew array<float>(3);

                FbxVector4 tangent = tangentLayers[j][i];
                Util::FbxVectorToArray(tangent, vertexData->Tangents[j]);

                FbxVector4 uv = uvLayers[j][i];
                Util::FbxVectorToArray(uv, vertexData->UVs[j]);
            }
        }

        FbxSkin* skin = nullptr;
        for (int i = 0; i < fbxMesh->GetDeformerCount(); i++)
        {
            if (fbxMesh->GetDeformer(i)->GetDeformerType() == FbxDeformer::eSkin) {
                skin = (FbxSkin*)fbxMesh->GetDeformer(i);
                break;
            }
        }

        if (skin != nullptr) {
            for (int i = 0; i < skin->GetClusterCount(); i++)
            {
                FbxCluster* cluster = skin->GetCluster(i);
                int* controlPointIndices = cluster->GetControlPointIndices();
                double* controlPointWeights = cluster->GetControlPointWeights();
                for (int j = 0; j < cluster->GetControlPointIndicesCount(); j++)
                {
                    FbxVertexData^ vertexData = meshData->VertexData[controlPointIndices[j]];
                    if (vertexData == nullptr) {
                        continue;
                    }

                    String^ boneName = gcnew String(cluster->GetLink()->GetName());
                    vertexData->BoneNames->Add(boneName);

                    float boneWeight = controlPointWeights[j];
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

