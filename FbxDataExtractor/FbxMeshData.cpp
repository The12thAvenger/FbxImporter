#include "pch.h"
#include <msclr/marshal.h>
#include <fbxsdk.h>
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
        FbxMeshData^ meshData = gcnew FbxMeshData(fbxMesh->GetName());

        if (!fbxMesh->IsTriangleMesh()) {
            throw gcnew System::IO::InvalidDataException(gcnew String("Mesh named " + meshData->Name + " is not triangulated. Please triangulate all meshes."));
        }

        fbxMesh->GenerateNormals();
        fbxMesh->GenerateTangentsDataForAllUVSets();

        FbxGeometryElementNormal* normalElement = fbxMesh->GetElementNormal();
        MapByPolygonVertex(normalElement);
        FbxLayerElementArrayTemplate<FbxVector4> normalsPerPoly = normalElement->GetDirectArray();

        const int numLayers = fbxMesh->GetLayerCount();
        std::vector<FbxLayerElementArrayTemplate<FbxVector4>> tangentLayers{ };
        std::vector<FbxLayerElementArrayTemplate<FbxVector2>> uvLayers{ };
        for (int i = 0; i < numLayers; i++)
        {
            FbxLayer* layer = fbxMesh->GetLayer(i);

            FbxLayerElementTangent* tangentElement = (FbxLayerElementTangent*)layer->GetLayerElementOfType(FbxLayerElement::EType::eTangent);
        	MapByPolygonVertex(tangentElement);
            tangentLayers.push_back(tangentElement->GetDirectArray());

            FbxLayerElementUV* uvElement = (FbxLayerElementUV*)layer->GetLayerElementOfType(FbxLayerElement::EType::eUV);
            MapByPolygonVertex(uvElement);
            uvLayers.push_back(uvElement->GetDirectArray());
        }

        const int numControlPoints = fbxMesh->GetControlPointsCount();
        std::vector<FbxVector4> normalsPerVertex(numControlPoints);
        std::vector<std::vector<FbxVector4>> tangentLayersPerVertex(numControlPoints);
        std::vector<std::vector<FbxVector2>> uvLayersPerVertex(numControlPoints);
        
        const int* vertexIndices = fbxMesh->GetPolygonVertices();
        for (int i = 0; i < fbxMesh->GetPolygonVertexCount(); i++)
        {
	        const int vertexIndex = vertexIndices[i];

            meshData->VertexIndices->Add(vertexIndex);

            normalsPerVertex.at(vertexIndex) = normalsPerPoly[i];

            for (int j = 0; j < numLayers; j++)
            {
                tangentLayersPerVertex.at(vertexIndex).push_back(tangentLayers.at(j)[i]);

                uvLayersPerVertex.at(vertexIndex).push_back(uvLayers.at(j)[i]);
            }
        }

        for (int i = 0; i < numControlPoints; i++)
        {
            if (!meshData->VertexIndices->Contains(i)) {
                meshData->VertexData->Add(nullptr);
                continue;
            }

            FbxVertexData^ vertexData = gcnew FbxVertexData();

            FbxVector4 position = fbxMesh->GetControlPointAt(i);
            Util::FbxVectorToArray(position, vertexData->Position);

            FbxVector4 normal = normalsPerVertex.at(i);
            Util::FbxVectorToArray(normal, vertexData->Normal);

            for (int j = 0; j < numLayers; j++)
            {
                vertexData->Tangents->Add(gcnew array<float>(4));
                vertexData->UVs->Add(gcnew array<float>(3));

                FbxVector4 tangent = tangentLayersPerVertex[i][j];
                Util::FbxVectorToArray(tangent, vertexData->Tangents[j]);

            	FbxVector2 uv = uvLayersPerVertex[i][j];
                array<float>^ uvData = vertexData->UVs[j];
                uvData[0] = uv.mData[0];
                uvData[1] = uv.mData[1];
            }

            meshData->VertexData->Add(vertexData);
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

    template <typename T>
    void FbxMeshData::MapByPolygonVertex(FbxLayerElementTemplate<T>* layerElementTemplate)
    {
        if (layerElementTemplate->GetMappingMode() == FbxLayerElementTemplate<T>::EMappingMode::eByPolygonVertex)
        {
            return;
        }

        int result = layerElementTemplate->RemapIndexTo(FbxLayerElement::EMappingMode::eByPolygonVertex);
        if (result == -1) {
            throw gcnew Exception("Remap failed. Result: " + result.ToString() + " Current Mode: " + ((int)layerElementTemplate->GetMappingMode()).ToString());
        }
    }
}

