import numpy as np

import torch
import torch.optim as optim

import torch.nn as nn
import torch.nn.functional as F
# can use the below import should you choose to initialize the weights of your Net
import torch.nn.init as I


class Net(nn.Module):

    def __init__(self):
        super(Net, self).__init__()
        
        ## TODO: Define all the layers of this CNN, the only requirements are:
        ## 1. This network takes in a square (same width and height), grayscale image as input
        ## 2. It ends with a linear layer that represents the keypoints
        ## it's suggested that you make this last layer output 136 values, 2 for each of the 68 keypoint (x, y) pairs
        
        # As an example, you've been given a convolutional layer, which you may (but don't have to) change:
        # 1 input image channel (grayscale), 32 output channels/feature maps, 5x5 square convolution kernel
        
        #obejctive is to bring down the image size to single unit-->
        #here given image size is 224x224px
        self.conv1 = nn.Conv2d(1, 32, 5)
        #224--> 224-5+1=220
        self.pool1 = nn.MaxPool2d(2, 2)
        #220/2=110 ...(32,110,110)
        
        self.conv2 = nn.Conv2d(32, 64, 3)
        #110--> 110-3+1=108
        self.pool2 = nn.MaxPool2d(2, 2)
        #108/2=54
        
        self.conv3 = nn.Conv2d(64, 128, 3)
        #54-->54-3+1=52
        self.pool3 = nn.MaxPool2d(2, 2)
        #52/2=26
        
        self.conv4 = nn.Conv2d(128, 256, 3)
        #26-->26-3+1=24
        self.pool4 = nn.MaxPool2d(2, 2)
        #24/2=12
        
        self.conv5 = nn.Conv2d(256,512,1)
        #12-->12-1+1=12
        self.pool5 = nn.MaxPool2d(2,2)
        #12/2=6
        
        #6x6x512
        self.fc1 = nn.Linear(6*6*512 , 1024)
#         self.fc2 = nn.Linear(1024,1024)
        self.fc2 = nn.Linear(1024, 36)
        
        self.drop1 = nn.Dropout(p = 0.1)
        self.drop2 = nn.Dropout(p = 0.2)
        self.drop3 = nn.Dropout(p = 0.3)
        self.drop4 = nn.Dropout(p = 0.4)
        self.drop5 = nn.Dropout(p = 0.5)
        self.drop6 = nn.Dropout(p = 0.6)
        #self.fc2_drop = nn.Dropout(p=.5)
            
        
        ## Note that among the layers to add, consider including:
        # maxpooling layers, multiple conv layers, fully-connected layers, and other layers (such as dropout or batch normalization) to avoid overfitting
        

        
    def forward(self, x):
        ## TODO: Define the feedforward behavior of this model
        ## x is the input image and, as an example, here you may choose to include a pool/conv step:
        x = self.drop1(self.pool1(F.relu(self.conv1(x))))
        x = self.drop2(self.pool2(F.relu(self.conv2(x))))
        x = self.drop3(self.pool3(F.relu(self.conv3(x))))
        x = self.drop4(self.pool4(F.relu(self.conv4(x))))
        x = self.drop5(self.pool5(F.relu(self.conv5(x))))
        x = x.view(x.size(0), -1)
        x = self.drop6(F.relu(self.fc1(x)))
        x = self.fc2(x)
        
        
        
        # a modified x, having gone through all the layers of your model, should be returned
        return x
    
    
def train_net(n_epochs, train_loader, net):
    
    #criterion = nn.CrossEntropyLoss()
    criterion = nn.MSELoss()

    #optimizer = optim.SGD(net.parameters(), lr=0.001, momentum=0.9)
    optimizer = optim.Adam(params = net.parameters(), lr = 0.001)
    # prepare the net for training
    net.train()

    for epoch in range(n_epochs):  # loop over the dataset multiple times
        running_loss = 0.0
        # train on batches of data, assumes you already have train_loader
        for batch_i, data in enumerate(train_loader):
            # get the input images and their corresponding labels
            images = data['image']
            key_pts = data['keypoints']

            # flatten pts
            key_pts = key_pts.view(key_pts.size(0), -1)

            # convert variables to floats for regression loss
            key_pts = key_pts.type(torch.FloatTensor)
            images = images.type(torch.FloatTensor)

            # forward pass to get outputs
            output_pts = net(images)
            #output_pts = output_pts.type(torch.FloatTensor)
            #print(output_pts.type)
            #print(key_pts.type)
            # calculate the loss between predicted and target keypoints
            print(key_pts.shape, output_pts.shape)
            loss = criterion(output_pts, key_pts)

            # zero the parameter (weight) gradients
            optimizer.zero_grad()
            
            # backward pass to calculate the weight gradients
            loss.backward()

            # update the weights
            optimizer.step()

            # print loss statistics
            running_loss += loss.item()
            print('Epoch: {}, Batch: {}, Avg. Loss: {}'.format(epoch + 1, batch_i+1, running_loss/1000))
                
    checkpoint = {'model': net.state_dict(), 'criterion':criterion, 'optimizer' : optimizer}
    torch.save(checkpoint, './saved_models/model_checkpoint_kpd.pt')

    print('Finished Training\n')
    
def test_net(test_loader, net):
    
    # iterate through the test dataset
    for i, sample in enumerate(test_loader):
        
        # get sample data: images and ground truth keypoints
        images = sample['image']
        key_pts = sample['keypoints']

        # convert images to FloatTensors
        images = images.type(torch.FloatTensor)
        

        # forward pass to get net output
        output_pts = net(images)
        
        # reshape to batch_size x 68 x 2 pts
        output_pts = output_pts.view(output_pts.size()[0], 18, -1)
        sample['output_pts'] = output_pts
        
        # break after first image is tested
        if i == 0:
            return images, output_pts, key_pts, sample
        
        print('Finished Predicting\n')