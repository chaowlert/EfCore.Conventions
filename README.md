![Icon](https://user-images.githubusercontent.com/5763993/64020551-18804500-cb5c-11e9-9259-7300faf6dc93.png)

# EfCore.Conventions
Setting EF Core by conventions

### Get it
```
PM> Install-Package EfCore.Conventions
```

### Setting table names
For example, if you would like to pluralize all table names.

First, you need to get `Inflector`.
```
PM> Install-Package Inflector.NetStandard
```

Then add this to your data context file.
```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    var inflector = new Inflector.Inflector(CultureInfo.GetCultureInfo("en-US"));
    modelBuilder.WithTableName(type => inflector.Pluralize(type.ClrType.Name));
}
```

### Setting default column types
For example, if you would like to set all `decimal` to `decimal(28, 2)`.

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.SetDefaultTypeNameForType<decimal>("decimal(28, 2)");
}
```

### Custom action based on property type
For example, if you would like to set `Conversion`.

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.ForPropertyType<Employee>(builder => builder.HasConversion(new JsonConverter<Employee>()));
}
```

### Custom action based on a predicate
For example, if you would like to set `Conversion` to all types annotated with `NotMappedAttribute`.

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.ForProperty(
        prop => prop.PropertyType.GetCustomAttribute<NotMappedAttribute>() != null,
        (builder, prop) => builder.HasConversion((ValueConverter)Activator.CreateInstance(typeof(JsonConverter<>).MakeGenericType(prop.PropertyType))));
}
```

### Extended Attributes

`EfCore.Conventions` also comes with following extended attributes. If you would like to apply the attributes, you need to first set your `modelBuilder`.

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.WithExtendedAttributes();
}
```

#### CompositeKey

If you would like to add composite key, you can annotate with `[CompositeKey]`.

For example.

```csharp
public class OrderItem
{
    [CompositeKey]
    public string OrderId { get; set; }

    [CompositeKey]
    public int RunningNo { get; set; }

    ...
}
```

You can also annotated with `[Column(Order = N)]` to set column order.

#### Index

If you would like to set index to column, you can annotate with `[Index]`.

For example,
```csharp
public class User
{
    [Key]
    public string Id { get; set; }

    [Index]
    public string Email { get; set; }

    ...
}
```

You can also set composite and/or unique index.

```csharp
public class Location
{
    [Index(Name = "IX_Zone_Area", Order = 1, IsUnique = true)]
    public string Zone { get; set; }

    [Index(Name = "IX_Zone_Area", Order = 2, IsUnique = true)]
    public string Area { get; set; }

    ...
}
```

#### OnDelete

You can set delete policy on column.

```csharp
public class OrderItem
{
    public string SkuId { get; set; }

    [OnDelete(DeleteBehavior.Restrict)]
    public Sku Sku { get; set; }

    ...
}
```
