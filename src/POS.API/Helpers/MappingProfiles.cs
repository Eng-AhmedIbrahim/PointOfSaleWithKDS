using System.Linq;
using AutoMapper;
using POS.Core.Entities;
using POS.Core.Entities.DineIn;
using POS.Core.Entities.OrderEntity;
using POS.Contract.Dtos.DineIn;
using POS.Contract.Dtos.OrderDtos;
using POS.Contract.Dtos.KitchenDtos;
using POS.Contract.Dtos.AccountDtos;
using POS.Core.Entities.UserSettings;
using BlazorBase.Models;

namespace POS.API.Helpers;

public class MappingProfiles : Profile
{
    public MappingProfiles()
    {
        CreateMap<CreateCompanyDto, Company>()
            .ForMember(c => c.NormalizedEnglishName,
            c =>
            c.MapFrom(c => c!.EnglishName!.ToUpper()))
           .ForMember(c => c.NormalizedEmail,
           c =>
           c.MapFrom(c => c!.Email!.ToUpper()));

        CreateMap<UpdatedCompanyDto, Company>()
           .ForMember(c => c.NormalizedEnglishName,
            c =>
            c.MapFrom(c => c!.EnglishName!.ToUpper()))
           .ForMember(c => c.NormalizedEmail,
           c =>
           c.MapFrom(c => c!.Email!.ToUpper()));


        CreateMap<BranchDto, Branch>()
           .ForMember(b => b.NormalizedName,
           b =>
           b.MapFrom(b => b!.Name!.ToUpper()));

        CreateMap<UpdatedBranchDto, Branch>()
           .ForMember(b => b.NormalizedName,
           b =>
           b.MapFrom(b => b!.Name!.ToUpper()));

        CreateMap<Branch, BranchToReturnDto>();

        CreateMap<CategoryDto, Category>()
            .ForMember(c => c.NormalizedEnglishName, c =>
            c.MapFrom(c => c!.EnglishName!.ToUpper()));

        CreateMap<UpdatedCategoryDto, Category>()
            .ForMember(c => c.NormalizedEnglishName, c => c.MapFrom(c => c!.EnglishName!.ToUpper()));


        CreateMap<CreateAttributeDto, Attributes>()
            .ForMember(a => a.Id, a => a.Ignore())
            .ForMember(dest => dest.AttributeGroups, opt => opt.MapFrom(src => src.AttributeGroups));

        CreateMap<AttributeGroup, AttributeGroupDto>().ReverseMap();

        CreateMap<AttributeItem, AttributeItemToReturnDto>()
            .ForMember(d => d.ItemNameArabic, o => o.MapFrom(s => s.RelatedMenuItem != null ? s.RelatedMenuItem.ArabicName : ""))
            .ForMember(d => d.ItemNameEnglish, o => o.MapFrom(s => s.RelatedMenuItem != null ? s.RelatedMenuItem.EnglishName : ""));
        CreateMap<Category, CategoryToReturnDto>();

        CreateMap<AttributeItemDto, AttributeItem>();
        CreateMap<ItemsClassifications, ItemsClassificationsDto>().ReverseMap();
        CreateMap<ItemType, ItemTypeDto>();

        CreateMap<MenuSalesItemsDto, MenuSalesItems>()
            .ForMember(s => s.ImagePath, s => s.Ignore())
            .ForMember(s => s.NormalizedEnglishName, s =>
            s.MapFrom(s => s!.EnglishName!.ToUpper()))
            .ForMember(i => i.AttributeId, i => i.MapFrom(i => i.AttributeId));





        CreateMap<UpdatedItemDto, MenuSalesItems>()
            .ForMember(s => s.NormalizedEnglishName, s =>
            s.MapFrom(s => s!.EnglishName!.ToUpper()));


        CreateMap<Attributes, AttributeToReturnDto>()
            .ForMember(dest => dest.AttributeItems, opt =>
                opt.MapFrom(src => src.AttributeItems))
            .ForMember(dest => dest.AttributeGroups, opt => opt.MapFrom(src => src.AttributeGroups));

        CreateMap<AttributeItem, AttributeItemToReturnDto>()
            .ForMember(d => d.ItemNameArabic, o => o.MapFrom(s => s.RelatedMenuItem != null ? s.RelatedMenuItem.ArabicName : ""))
            .ForMember(d => d.ItemNameEnglish, o => o.MapFrom(s => s.RelatedMenuItem != null ? s.RelatedMenuItem.EnglishName : ""));


        CreateMap<UpdatedAttributeDto, Attributes>()
            .ForMember(dest => dest.AttributeItems, opt =>
                opt.MapFrom(src => src.AttributeItems))
            .ForMember(dest => dest.AttributeGroups, opt => opt.MapFrom(src => src.AttributeGroups));

        CreateMap<MenuSalesItems, MenuSalesItemsToReturnDto>()
            .ForMember(s => s.ImageUrl,
                s => s.MapFrom<ImageUrlResolver<MenuSalesItems, MenuSalesItemsToReturnDto>>())
            .ForMember(dest => dest.PrintInBackupReceiptFromCategory,
                src => src.MapFrom(s => s.Category != null ? s.Category.PrintInBackupReceipt : null))
            .ForMember(dest => dest.CategoryKitchenTypeId,
                src => src.MapFrom(i => i.Category != null ? i.Category.KitchenTypeId : null))
    .ForMember(dest => dest.Attributes,
        opt => opt.MapFrom(src =>
            src.HasAttribute && src.Attribute != null
                ? src.Attribute.AttributeItems.Select(ai => new MenuSalesItemAttributes
                {
                    AppearanceIndex = ai.AppearanceIndex,
                    GroupItems = new List<MenuSalesItemsGroupDto>
                    {
                        new MenuSalesItemsGroupDto
                        {
                            Id = ai.Id,
                            ArabicName = ai.RelatedMenuItem!.ArabicName,
                            EnglishName = ai.RelatedMenuItem.EnglishName,

                            Price = ai.RelatedMenuItem != null ? ai.RelatedMenuItem.Price : 0,
                            ExtraPrice = ai.ExtraPrice,
                            ByWeight = ai.RelatedMenuItem != null && ai.RelatedMenuItem.ByWeight,
                            RelatedMenuItemId = ai.RelatedMenuItemId
                        }
                    }
                }).ToList()
                : null
        ));


        CreateMap<AppUser, UserDto>()
            .ForMember(dest => dest.UserId, src => src.MapFrom(s => s.Id))
            .ForMember(dest => dest.ArabicName, src => src.MapFrom(s => s.ArabicName))
            .ForMember(dest => dest.DisplayName, src => src.MapFrom(s => s.DisplayName))
            .ForMember(dest => dest.PhoneNumber, src => src.MapFrom(s => s.PhoneNumber))
            .ForMember(dest => dest.IsActive, src => src.MapFrom(s => s.IsActive))
            .ForMember(dest => dest.DateOfBirth, src => src.MapFrom(s => s.DateOfBirth));

        CreateMap<TableDto, Table>();
        CreateMap<TableGroupDto, TableGroup>();
        CreateMap<TableGroupToReturnDto, TableGroup>().ReverseMap();
        CreateMap<Table, TableToReturnDto>()
            .ForMember(x => x.TableState, src => src.MapFrom(s => s.TableState.ToString()))
            .ReverseMap();


        CreateMap<AppUser, UserToReturnDto>();
        CreateMap<ApplicationRole, RoleToReturnDto>();
        CreateMap<Permission, PermissionDto>();


        CreateMap<AppDate, AppDateToReturnDto>();

        CreateMap<DeliveryZone, DeliveryZonesToReturnDto>().ReverseMap();
        CreateMap<DeliveryZoneDto, DeliveryZone>().ReverseMap();


        CreateMap<DeliveryCustomerInfo, DeliveryCustomerToReturnDto>()
        .ForMember(dest => dest.CustomerAddresses,
             opt =>
                opt.MapFrom(src => src.CustomerAddresses));

        CreateMap<CustomerAddress, CustomerAddressDto>()
            .ForMember(d => d.DeliveryFee, o => o.MapFrom(s => s.DeliveryZone != null ? s.DeliveryZone.DeliveryFee : 0))
            .ForMember(d => d.ZoneName, o => o.MapFrom(s => s.DeliveryZone != null ? s.DeliveryZone.ZoneName : s.ZoneName))
            .ForMember(d => d.BranchName, o => o.MapFrom(s => s.Branch != null ? s.Branch.Name : s.BranchName))
            .ForMember(d => d.ZoneBonus, o => o.MapFrom(s => s.DeliveryZone != null ? s.DeliveryZone.ZoneBonus : 0));


        CreateMap<DeliveryCustomerDto, DeliveryCustomerInfo>()
            .ForMember(dest => dest.CustomerAddresses, opt => opt.MapFrom(src => new List<CustomerAddress>
            {
                new CustomerAddress
                {
                    BranchName = src.BranchName,
                    ZoneName = src.ZoneName,
                    HomeNumber = src.HomeNumber,
                    FloorNumber = src.FloorNumber,
                    FlatNumber = src.FlatNumber,
                    ClientAddress = src.ClientAddress,
                    AddressNote = src.AddressNote,
                    DeliveryZoneId = src.DeliveryZoneId ?? 0,
                    BranchId = src.BranchId ?? 0
                }
            }));

        CreateMap<DeliveryCustomerInfo, DeliveryCustomerDto>()
          .ForMember(dest => dest.ClientAddress,
          opt =>
          opt.MapFrom(src => src.CustomerAddresses));


        CreateMap<CustomerNewAddressDto, CustomerAddress>();


        CreateMap<KitchenType, KitchenTypeToReturnDto>()
               .ForMember(dest => dest.KitchenPrinter, opt
               => opt.MapFrom(src => src.KitchenPrinters != null ? src.KitchenPrinters.FirstOrDefault() : null));

        CreateMap<KitchenPrinters, KitchenPrintersDto>().ReverseMap();


        CreateMap<KitchenTypeDto, KitchenType>();
        CreateMap<KitchenPrinters, KitchenPrintersToReturnDto>();


        CreateMap<Orders, OrderDto>()
            .ForMember(dest => dest.OrderId, opt => opt.MapFrom(src => src.OrderID))
            .ForMember(dest => dest.BranchId, opt => opt.MapFrom(src => src.BranchID))
            .ForMember(dest => dest.TakeAwayCustomerName, opt => opt.MapFrom(src => src.TakeawayCustomerName))
            .ForMember(dest => dest.TakeawayCustomerPhone, opt => opt.MapFrom(src => src.TakeawayCustomerPhone))
            .ForMember(dest => dest.TotalOrderDiscount, opt => opt.MapFrom(src => src.TotalDiscount))
            .ForMember(dest => dest.Remaining, opt => opt.MapFrom(src => src.Remain))
            .ForMember(dest => dest.Services, opt => opt.MapFrom(src => src.Service))
            .ForMember(dest => dest.OrderType, opt => opt.MapFrom(src => src.OrderType.HasValue ? src.OrderType.Value.ToString() : null))
            .ForMember(dest => dest.OrderState, opt => opt.MapFrom(src => src.OrderState.HasValue ? src.OrderState.Value.ToString() : null))
            .ForMember(dest => dest.OrderDetails, opt => opt.MapFrom(src => src.OrderDetails)) // هنعمل مابنج تاني للجوه
            .ForMember(dest => dest.MachineName, opt => opt.MapFrom(src => src.MachineName))
            .ForMember(dest => dest.OrderDate, opt => opt.MapFrom(src => src.OrderDate))
            .ForMember(dest => dest.CallCenterOrderId, opt => opt.MapFrom(src => src.CallCenterOrderId))
            .ForMember(dest => dest.RemoteOrderId, opt => opt.MapFrom(src => src.RemoteOrderId))
            .ForMember(dest => dest.IsHospitality, opt => opt.MapFrom(src => src.IsHospitality))
            .ForMember(dest => dest.HospitalityResponsibleId, opt => opt.MapFrom(src => src.HospitalityResponsibleId))
            .ForMember(dest => dest.HospitalityResponsibleName, opt => opt.MapFrom(src => src.HospitalityResponsibleName))
            .ForMember(dest => dest.HospitalityReason, opt => opt.MapFrom(src => src.HospitalityReason))
            .ForMember(dest => dest.IsStaffMeal, opt => opt.MapFrom(src => src.IsStaffMeal))
            .ForMember(dest => dest.StaffMealEmployeeId, opt => opt.MapFrom(src => src.StaffMealEmployeeId))
            .ForMember(dest => dest.StaffMealEmployeeName, opt => opt.MapFrom(src => src.StaffMealEmployeeName))
            // ─── Fix case mismatch: Entity.Subtotal → Dto.SubTotal ───
            .ForMember(dest => dest.SubTotal, opt => opt.MapFrom(src => src.Subtotal))
            // ─── TotalDiscount explicit (same name but belt-and-suspenders) ───
            .ForMember(dest => dest.TotalDiscount, opt => opt.MapFrom(src => src.TotalDiscount))
            // ─── Discount details ───
            .ForMember(dest => dest.DiscountType, opt => opt.MapFrom(src => src.DiscountType))
            .ForMember(dest => dest.DiscountPercentage, opt => opt.MapFrom(src => src.DiscountPercentage))
            .ForMember(dest => dest.DiscountBy, opt => opt.MapFrom(src => src.DiscountBy))
            .ForMember(dest => dest.DiscountByName, opt => opt.MapFrom(src => src.DiscountByName))
            .ForMember(dest => dest.DiscountTime, opt => opt.MapFrom(src => src.DiscountTime))
            .ForMember(dest => dest.DiscountReason, opt => opt.MapFrom(src => src.DiscountReason))
            // ─── Void details ───
            .ForMember(dest => dest.VoidAmount, opt => opt.MapFrom(src => src.VoidAmount))
            .ForMember(dest => dest.VoidBy, opt => opt.MapFrom(src => src.VoidBy))
            .ForMember(dest => dest.VoidByName, opt => opt.MapFrom(src => src.VoidByName))
            .ForMember(dest => dest.VoidTime, opt => opt.MapFrom(src => src.VoidTime))
            .ForMember(dest => dest.VoidReason, opt => opt.MapFrom(src => src.VoidReason))
            .ForMember(dest => dest.VoidCount, opt => opt.MapFrom(src => src.VoidCount))
            .ForMember(dest => dest.TotalVoid, opt => opt.MapFrom(src => src.TotalVoid));

        CreateMap<OrderItemsDetails, TableItem>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.MenuSalesItemId))
            .ForMember(dest => dest.DatabaseId, opt => opt.MapFrom(src => src.Id))
            .ForMember(dest => dest.Name, opt => opt.MapFrom(src => !string.IsNullOrEmpty(src.ItemName) ? src.ItemName : (src.MenuSalesItem != null ? src.MenuSalesItem.EnglishName : "")))
            .ForMember(dest => dest.NameAr, opt => opt.MapFrom(src => !string.IsNullOrEmpty(src.ItemNameAr) ? src.ItemNameAr : (src.MenuSalesItem != null ? src.MenuSalesItem.ArabicName : "")))
            .ForMember(dest => dest.DiscountPercentage, opt => opt.MapFrom(src => src.TotalDiscountPercentage))
            .ForMember(dest => dest.DiscountReason, opt => opt.MapFrom(src => src.DiscountReason))
            .ForMember(dest => dest.Price, opt => opt.MapFrom(src => src.UnitPrice))
            .ForMember(dest => dest.Total, opt => opt.MapFrom(src => src.TotalAmount))
            .ForMember(dest => dest.ItemKitchenTypeId, opt => opt.MapFrom(src => src.ItemKitchenTypeId))
            .ForMember(dest => dest.CategoryKitchenTypeId, opt => opt.MapFrom(src => src.CategoryKitchenTypeId))
            .ForMember(dest => dest.CategoryName, opt => opt.MapFrom(src => src.CategoryName ?? (src.MenuSalesItem != null && src.MenuSalesItem.Category != null ? src.MenuSalesItem.Category.ArabicName : "")))
            .ForMember(dest => dest.PrintInBackupReceiptFromItem, opt => opt.MapFrom(src => src.PrintInBackupReceiptFromItem))
            .ForMember(dest => dest.PrintInBackupReceiptFromCategory, opt => opt.MapFrom(src => src.PrintInBackupReceiptFromCategory))
            .ForMember(dest => dest.ByWeight, opt => opt.MapFrom(src => src.ByWeight ?? false))
            .ForMember(dest => dest.IsHospitality, opt => opt.MapFrom(src => src.IsHospitality))
            .ForMember(dest => dest.HospitalityResponsibleName, opt => opt.MapFrom(src => src.HospitalityResponsibleName))
            .ForMember(dest => dest.IsStaffMeal, opt => opt.MapFrom(src => src.IsStaffMeal))
            .ForMember(dest => dest.StaffName, opt => opt.MapFrom(src => src.StaffMealEmployeeName))
            .ForMember(dest => dest.LineComment, opt => opt.MapFrom(src => src.LineComment))
            .ForMember(dest => dest.Attributes, opt => opt.MapFrom(src => src.OrderItemAttributes != null 
                ? src.OrderItemAttributes.Select(a => new POS.Contract.Models.AttributeDto { Id = a.AttributeItemId, Name = a.AttributeName, ExtraPrice = a.ExtraPrice }).ToList() 
                : new List<POS.Contract.Models.AttributeDto>()));

        // Reverse mapping: TableItem → OrderItemsDetails (used when syncing order updates between CC and Branch)
        CreateMap<TableItem, OrderItemsDetails>()
            .ForMember(dest => dest.MenuSalesItemId, opt => opt.MapFrom(src => src.Id))
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.DatabaseId))
            .ForMember(dest => dest.ItemName, opt => opt.MapFrom(src => src.Name))
            .ForMember(dest => dest.ItemNameAr, opt => opt.MapFrom(src => src.NameAr))
            .ForMember(dest => dest.UnitPrice, opt => opt.MapFrom(src => src.Price))
            .ForMember(dest => dest.TotalAmount, opt => opt.MapFrom(src => src.Total))
            .ForMember(dest => dest.TotalDiscountPercentage, opt => opt.MapFrom(src => src.DiscountPercentage))
            .ForMember(dest => dest.DiscountReason, opt => opt.MapFrom(src => src.DiscountReason))
            .ForMember(dest => dest.CategoryName, opt => opt.MapFrom(src => src.CategoryName))
            .ForMember(dest => dest.ItemKitchenTypeId, opt => opt.MapFrom(src => src.ItemKitchenTypeId))
            .ForMember(dest => dest.CategoryKitchenTypeId, opt => opt.MapFrom(src => src.CategoryKitchenTypeId))
            .ForMember(dest => dest.PrintInBackupReceiptFromItem, opt => opt.MapFrom(src => src.PrintInBackupReceiptFromItem))
            .ForMember(dest => dest.PrintInBackupReceiptFromCategory, opt => opt.MapFrom(src => src.PrintInBackupReceiptFromCategory))
            .ForMember(dest => dest.ByWeight, opt => opt.MapFrom(src => src.ByWeight))
            .ForMember(dest => dest.IsHospitality, opt => opt.MapFrom(src => src.IsHospitality))
            .ForMember(dest => dest.HospitalityResponsibleName, opt => opt.MapFrom(src => src.HospitalityResponsibleName))
            .ForMember(dest => dest.IsStaffMeal, opt => opt.MapFrom(src => src.IsStaffMeal))
            .ForMember(dest => dest.StaffMealEmployeeName, opt => opt.MapFrom(src => src.StaffName))
            .ForMember(dest => dest.LineComment, opt => opt.MapFrom(src => src.LineComment))
            .ForMember(dest => dest.OrderItemAttributes, opt => opt.MapFrom(src => src.Attributes != null
                ? src.Attributes.Select(a => new POS.Core.Entities.Item.OrderItemAttributes
                    { AttributeItemId = a.Id ?? 0, AttributeName = a.Name ?? "", ExtraPrice = a.ExtraPrice ?? 0 }).ToList()
                : new List<POS.Core.Entities.Item.OrderItemAttributes>()))
            .ForMember(dest => dest.OrderItemComments, opt => opt.Ignore())
            .ForMember(dest => dest.MenuSalesItem, opt => opt.Ignore())
            .ForMember(dest => dest.Order, opt => opt.Ignore())
            .ForMember(dest => dest.DineInOrder, opt => opt.Ignore())
            .ForMember(dest => dest.DineInOrderId, opt => opt.Ignore());

        // New mappings for DineInOrder and OrderTrack
        CreateMap<OrderItemsDetails, OrderItemsDetailsDto>()
            .ForMember(dest => dest.ItemName, opt => opt.MapFrom(src => src.ItemName ?? (src.MenuSalesItem != null ? src.MenuSalesItem.ArabicName : "")))
            .ForMember(dest => dest.ItemNameAr, opt => opt.MapFrom(src => src.ItemNameAr ?? (src.MenuSalesItem != null ? src.MenuSalesItem.ArabicName : "")))
            .ForMember(dest => dest.CategoryId, opt => opt.MapFrom(src => src.CategoryId ?? (src.MenuSalesItem != null ? src.MenuSalesItem.CategoryId : null)))
            .ForMember(dest => dest.CategoryName, opt => opt.MapFrom(src => src.CategoryName ?? (src.MenuSalesItem != null && src.MenuSalesItem.Category != null ? src.MenuSalesItem.Category.ArabicName : "")))
            .ForMember(dest => dest.Price, opt => opt.MapFrom(src => src.UnitPrice ?? (src.MenuSalesItem != null ? src.MenuSalesItem.Price : 0)))
            .ForMember(dest => dest.OrderItemComments, opt => opt.MapFrom(src => src.OrderItemComments))
            // Map Kitchen Printing Properties - Use persisted values first, fallback to MenuSalesItem if null
            .ForMember(dest => dest.ItemKitchenTypeId, opt => opt.MapFrom(src => src.ItemKitchenTypeId))
            .ForMember(dest => dest.CategoryKitchenTypeId, opt => opt.MapFrom(src => src.CategoryKitchenTypeId))
            .ForMember(dest => dest.PrintInBackupReceiptFromItem, opt => opt.MapFrom(src => src.PrintInBackupReceiptFromItem))
            .ForMember(dest => dest.PrintInBackupReceiptFromCategory, opt => opt.MapFrom(src => src.PrintInBackupReceiptFromCategory))
            .ForMember(dest => dest.IsHospitality, opt => opt.MapFrom(src => src.IsHospitality))
            .ForMember(dest => dest.HospitalityResponsibleName, opt => opt.MapFrom(src => src.HospitalityResponsibleName))
            .ForMember(dest => dest.IsStaffMeal, opt => opt.MapFrom(src => src.IsStaffMeal))
            .ForMember(dest => dest.StaffMealEmployeeName, opt => opt.MapFrom(src => src.StaffMealEmployeeName))
            .ForMember(dest => dest.LineComment, opt => opt.MapFrom(src => src.LineComment))
            .ReverseMap()
            .ForMember(dest => dest.UnitPrice, opt => opt.MapFrom(src => src.Price))
            .ForMember(dest => dest.OrderItemComments, opt => opt.MapFrom(src => src.OrderItemComments))
            .ForMember(dest => dest.LineComment, opt => opt.MapFrom(src => src.LineComment))
            // Map Kitchen properties FROM DTO TO Entity
            .ForMember(dest => dest.ItemKitchenTypeId, opt => opt.MapFrom(src => src.ItemKitchenTypeId))
            .ForMember(dest => dest.CategoryKitchenTypeId, opt => opt.MapFrom(src => src.CategoryKitchenTypeId))
            .ForMember(dest => dest.PrintInBackupReceiptFromItem, opt => opt.MapFrom(src => src.PrintInBackupReceiptFromItem))
            .ForMember(dest => dest.PrintInBackupReceiptFromCategory, opt => opt.MapFrom(src => src.PrintInBackupReceiptFromCategory))
            .ForMember(dest => dest.IsHospitality, opt => opt.MapFrom(src => src.IsHospitality))
            .ForMember(dest => dest.HospitalityResponsibleName, opt => opt.MapFrom(src => src.HospitalityResponsibleName))
            .ForMember(dest => dest.IsStaffMeal, opt => opt.MapFrom(src => src.IsStaffMeal))
            .ForMember(dest => dest.StaffMealEmployeeName, opt => opt.MapFrom(src => src.StaffMealEmployeeName));

        CreateMap<OrderItemComment, OrderItemCommentDto>().ReverseMap();
        CreateMap<OrderItemAttributes, OrderItemAttributesDto>().ReverseMap();
        CreateMap<OrderTrack, OrderTrackDto>().ReverseMap();

        CreateMap<Orders, DineInOrderDto>()
            .ForMember(dest => dest.OrderId, opt => opt.MapFrom(src => src.OrderID))
            .ForMember(dest => dest.BranchId, opt => opt.MapFrom(src => src.BranchID))
            .ForMember(dest => dest.OrderDateTime, opt => opt.MapFrom(src => src.OrderDate))
            .ForMember(dest => dest.OrderState, opt => opt.MapFrom(src => src.OrderState.HasValue ? src.OrderState.Value.ToString() : "Open"))
            .ForMember(dest => dest.DiscountAmount, opt => opt.MapFrom(src => src.Discount))
            .ForMember(dest => dest.TotalDiscount, opt => opt.MapFrom(src => src.TotalDiscount))
            .ForMember(dest => dest.Subtotal, opt => opt.MapFrom(src => src.Subtotal))
            .ForMember(dest => dest.GrandTotal, opt => opt.MapFrom(src => src.GrandTotal))
            .ForMember(dest => dest.Tax, opt => opt.MapFrom(src => src.Tax))
            .ForMember(dest => dest.Service, opt => opt.MapFrom(src => src.Service))
            .ForMember(dest => dest.TableId, opt => opt.MapFrom(src => src.TableID))
            .ForMember(dest => dest.TableName, opt => opt.MapFrom(src => src.TableName))
            .ForMember(dest => dest.CaptainName, opt => opt.MapFrom(src => src.WaiterName))
            .ForMember(dest => dest.CaptainId, opt => opt.MapFrom(src => src.WaiterID))
            .ForMember(dest => dest.OrderDetails, opt => opt.MapFrom(src => src.OrderDetails))
            .ForMember(dest => dest.MachineName, opt => opt.MapFrom(src => src.MachineName))
            .ForMember(dest => dest.CustomerCount, opt => opt.MapFrom(src => src.CustomerCount))
            .ForMember(dest => dest.MaleCount, opt => opt.MapFrom(src => src.MaleCount))
            .ForMember(dest => dest.FemaleCount, opt => opt.MapFrom(src => src.FemaleCount))
            .ForMember(dest => dest.DiscountedItems, opt => opt.MapFrom(src => src.DiscountedItems))
            .ForMember(dest => dest.ReservationCustomerName, opt => opt.MapFrom(src => src.ReservationCustomerName))
            .ForMember(dest => dest.ReservationCustomerPhone, opt => opt.MapFrom(src => src.ReservationCustomerPhone))
            .ForMember(dest => dest.ReservationPaid, opt => opt.MapFrom(src => src.ReservationPaid))
            .ForMember(dest => dest.ScheduleDateTime, opt => opt.MapFrom(src => src.ScheduleDateTime))
            .ForMember(dest => dest.MaleCount, opt => opt.MapFrom(src => src.MaleCount))
            .ForMember(dest => dest.FemaleCount, opt => opt.MapFrom(src => src.FemaleCount))
            .ForMember(dest => dest.IsHospitality, opt => opt.MapFrom(src => src.IsHospitality))
            .ForMember(dest => dest.HospitalityResponsibleId, opt => opt.MapFrom(src => src.HospitalityResponsibleId))
            .ForMember(dest => dest.HospitalityResponsibleName, opt => opt.MapFrom(src => src.HospitalityResponsibleName))
            .ForMember(dest => dest.HospitalityReason, opt => opt.MapFrom(src => src.HospitalityReason))
            .ForMember(dest => dest.IsStaffMeal, opt => opt.MapFrom(src => src.IsStaffMeal))
            .ForMember(dest => dest.StaffMealEmployeeId, opt => opt.MapFrom(src => src.StaffMealEmployeeId))
            .ForMember(dest => dest.StaffMealEmployeeName, opt => opt.MapFrom(src => src.StaffMealEmployeeName))
            .ReverseMap()
            .ForMember(dest => dest.OrderID, opt => opt.MapFrom(src => src.OrderId))
            .ForMember(dest => dest.BranchID, opt => opt.MapFrom(src => src.BranchId))
            .ForMember(dest => dest.OrderDate, opt => opt.MapFrom(src => src.OrderDateTime))
            .ForMember(dest => dest.Discount, opt => opt.MapFrom(src => src.DiscountAmount))
            .ForMember(dest => dest.TotalDiscount, opt => opt.MapFrom(src => src.TotalDiscount))
            .ForMember(dest => dest.DiscountedItems, opt => opt.MapFrom(src => src.DiscountedItems))
            .ForMember(dest => dest.TableID, opt => opt.MapFrom(src => src.TableId))
            .ForMember(dest => dest.TableName, opt => opt.MapFrom(src => src.TableName))
            .ForMember(dest => dest.WaiterName, opt => opt.MapFrom(src => src.CaptainName))
            .ForMember(dest => dest.WaiterID, opt => opt.MapFrom(src => src.CaptainId))
            .ForMember(dest => dest.TableState, opt => opt.MapFrom(src => src.OrderState))
            .ForMember(dest => dest.OrderType, opt => opt.MapFrom(src => OrderTypes.DineIn))
            .ForMember(dest => dest.ReservationCustomerName, opt => opt.MapFrom(src => src.ReservationCustomerName))
            .ForMember(dest => dest.ReservationCustomerPhone, opt => opt.MapFrom(src => src.ReservationCustomerPhone))
            .ForMember(dest => dest.ReservationPaid, opt => opt.MapFrom(src => src.ReservationPaid))
            .ForMember(dest => dest.ScheduleDateTime, opt => opt.MapFrom(src => src.ScheduleDateTime))
            .ForMember(dest => dest.MaleCount, opt => opt.MapFrom(src => src.MaleCount))
            .ForMember(dest => dest.FemaleCount, opt => opt.MapFrom(src => src.FemaleCount))
            .ForMember(dest => dest.IsHospitality, opt => opt.MapFrom(src => src.IsHospitality))
            .ForMember(dest => dest.HospitalityResponsibleId, opt => opt.MapFrom(src => src.HospitalityResponsibleId))
            .ForMember(dest => dest.HospitalityResponsibleName, opt => opt.MapFrom(src => src.HospitalityResponsibleName))
            .ForMember(dest => dest.HospitalityReason, opt => opt.MapFrom(src => src.HospitalityReason))
            .ForMember(dest => dest.IsStaffMeal, opt => opt.MapFrom(src => src.IsStaffMeal))
            .ForMember(dest => dest.StaffMealEmployeeId, opt => opt.MapFrom(src => src.StaffMealEmployeeId))
            .ForMember(dest => dest.StaffMealEmployeeName, opt => opt.MapFrom(src => src.StaffMealEmployeeName))
            .ForMember(dest => dest.OrderState, opt => opt.MapFrom(src => src.OrderState == "Open" ? OrderStates.Pending : 
                                                                  src.OrderState == "Reserved" ? OrderStates.Reserved : OrderStates.Completed));

        CreateMap<PosFeatureSetting, POS.Contract.Dtos.OrderDto.PosFeatureSettingToReturnDto>().ReverseMap();
        CreateMap<OrderSetting, POS.Contract.Dtos.OrderDto.OrderSettingToReturnDto>().ReverseMap();

        // Expense mapping
        CreateMap<POS.Core.Entities.Payment.Expense, POS.Contract.Dtos.ReportingDtos.ExpenseDto>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id))
            .ForMember(dest => dest.Date, opt => opt.MapFrom(src => src.Date))
            .ForMember(dest => dest.Amount, opt => opt.MapFrom(src => src.Amount))
            .ForMember(dest => dest.Description, opt => opt.MapFrom(src => src.Description))
            .ForMember(dest => dest.Category, opt => opt.MapFrom(src => src.Category ?? string.Empty))
            .ForMember(dest => dest.CreatedById, opt => opt.MapFrom(src => src.CreatedById))
            .ForMember(dest => dest.CreatedByName, opt => opt.MapFrom(src => src.CreatedByName))
            .ForMember(dest => dest.SpentBy, opt => opt.MapFrom(src => src.SpentBy))
            .ForMember(dest => dest.IsPayoutFromDrawer, opt => opt.MapFrom(src => src.IsPayoutFromDrawer))
            .ForMember(dest => dest.BranchId, opt => opt.MapFrom(src => src.BranchId))
            .ForMember(dest => dest.ShiftId, opt => opt.MapFrom(src => src.ShiftId));
    }
}

