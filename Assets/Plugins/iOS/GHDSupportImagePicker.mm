#import <Foundation/Foundation.h>
#import <PhotosUI/PhotosUI.h>
#import <UniformTypeIdentifiers/UniformTypeIdentifiers.h>
#import <UIKit/UIKit.h>

extern UIViewController *UnityGetGLViewController(void);
extern void UnitySendMessage(const char *obj, const char *method, const char *msg);

static NSString *const GHDUnityObject = @"Gravity Half Dead \u00b7 App Flow";
static const unsigned long long GHDMaximumBytes = 10ULL * 1024ULL * 1024ULL;

static void GHDSend(NSString *method, NSString *message) {
    dispatch_async(dispatch_get_main_queue(), ^{
        UnitySendMessage(GHDUnityObject.UTF8String, method.UTF8String,
                         (message ?: @"").UTF8String);
    });
}

API_AVAILABLE(ios(14.0))
@interface GHDSupportPickerDelegate : NSObject <PHPickerViewControllerDelegate>
@end

@implementation GHDSupportPickerDelegate
- (void)picker:(PHPickerViewController *)picker didFinishPicking:(NSArray<PHPickerResult *> *)results {
    [picker dismissViewControllerAnimated:YES completion:nil];
    if (results.count == 0) {
        GHDSend(@"OnSupportImagePickerError", @"Image selection was cancelled.");
        return;
    }

    dispatch_group_t group = dispatch_group_create();
    NSMutableArray<NSString *> *paths = [NSMutableArray array];
    __block unsigned long long totalBytes = 0;
    __block NSString *failure = nil;
    NSURL *directory = [[NSFileManager defaultManager].temporaryDirectory
                        URLByAppendingPathComponent:@"gravity-support-images" isDirectory:YES];
    [[NSFileManager defaultManager] createDirectoryAtURL:directory
                             withIntermediateDirectories:YES attributes:nil error:nil];

    [results enumerateObjectsUsingBlock:^(PHPickerResult *result, NSUInteger index, BOOL *stop) {
        dispatch_group_enter(group);
        [result.itemProvider loadFileRepresentationForTypeIdentifier:UTTypeImage.identifier
                                                   completionHandler:^(NSURL *url, NSError *error) {
            @autoreleasepool {
                if (failure == nil && (error != nil || url == nil))
                    failure = @"An image could not be read.";
                if (failure == nil) {
                    NSNumber *size = nil;
                    [url getResourceValue:&size forKey:NSURLFileSizeKey error:nil];
                    @synchronized (paths) {
                        totalBytes += size.unsignedLongLongValue;
                        if (totalBytes > GHDMaximumBytes) {
                            failure = @"Selected images exceed the 10 MB combined limit.";
                        } else {
                            NSString *extension = url.pathExtension.length > 0 ? url.pathExtension : @"jpg";
                            NSString *name = [NSString stringWithFormat:@"support-%@-%lu.%@",
                                              NSUUID.UUID.UUIDString, (unsigned long)index, extension];
                            NSURL *destination = [directory URLByAppendingPathComponent:name];
                            NSError *copyError = nil;
                            [[NSFileManager defaultManager] copyItemAtURL:url toURL:destination error:&copyError];
                            if (copyError != nil)
                                failure = @"An image could not be copied.";
                            else
                                [paths addObject:destination.path];
                        }
                    }
                }
                dispatch_group_leave(group);
            }
        }];
    }];

    dispatch_group_notify(group, dispatch_get_main_queue(), ^{
        if (failure != nil)
            GHDSend(@"OnSupportImagePickerError", failure);
        else
            GHDSend(@"OnSupportImagesPicked", [paths componentsJoinedByString:@"\n"]);
    });
}
@end

static GHDSupportPickerDelegate *GHDPickerDelegate;

extern "C" void GHD_PickSupportImages(void) {
    dispatch_async(dispatch_get_main_queue(), ^{
        if (@available(iOS 14.0, *)) {
            PHPickerConfiguration *configuration = [[PHPickerConfiguration alloc] init];
            configuration.filter = PHPickerFilter.imagesFilter;
            configuration.selectionLimit = 0;
            PHPickerViewController *picker = [[PHPickerViewController alloc] initWithConfiguration:configuration];
            GHDPickerDelegate = [GHDSupportPickerDelegate new];
            picker.delegate = GHDPickerDelegate;
            [UnityGetGLViewController() presentViewController:picker animated:YES completion:nil];
        } else {
            GHDSend(@"OnSupportImagePickerError", @"Multiple image selection requires iOS 14 or newer.");
        }
    });
}
